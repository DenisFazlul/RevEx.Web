using MongoDB.Bson;
using MongoDB.Driver;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Domain.Entities;
using RevEx.Infrastructure.Persistence;
using RevEx.Infrastructure.Persistence.Migrations;

internal static class FileMetadataChecks
{
    public static async Task RunAsync(IServiceProvider services, string database)
    {
        using var scope = services.CreateScope();
        var files = scope.ServiceProvider.GetRequiredService<IContentFileRepository>();
        var metadata = scope.ServiceProvider.GetRequiredService<IContentFileMetadataRepository>();
        var context = scope.ServiceProvider.GetRequiredService<MongoDbContext>();
        var mongo = new MongoClient("mongodb://localhost:27017").GetDatabase(database);
        var rawFiles = mongo.GetCollection<BsonDocument>("contentFiles");
        var rawMetadata = mongo.GetCollection<BsonDocument>("contentFileMetadata");
        const string json = """{"RevitCategory":"Двери","Types":[],"future":{"nested":[1,true,null]}}""";

        var withoutMetadata = await files.AddAsync(CreateFile("without.rfa"), default);
        var withMetadata = CreateFile("with.rfa");
        withMetadata.MetadataJson = json;
        await files.AddAsync(withMetadata, default);
        var stored = await metadata.GetByFileIdAsync(withMetadata.Id, default);
        Require(stored is not null && stored.Id != stored.FileId, "Metadata identity must be independent of the file identity.");
        Require(stored!.FileId == withMetadata.Id && BsonDocument.Parse(stored.MetadataJson).Equals(BsonDocument.Parse(json)), "Metadata was not linked by FileId.");
        Require((await files.GetByIdAsync(withoutMetadata.Id, default))!.MetadataJson is null, "A file without metadata received another file's metadata.");
        var list = await files.GetAllAsync(default);
        Require(list.Single(file => file.Id == withoutMetadata.Id).MetadataJson is null
            && list.Single(file => file.Id == withMetadata.Id).MetadataJson is not null, "Batch metadata loading used document position instead of FileId.");

        var rejectedDuplicate = false;
        try
        {
            await metadata.AddAsync(new ContentFileMetadata { FileId = withMetadata.Id, MetadataJson = "{}" }, default);
        }
        catch (Exception exception) when (exception.ToString().Contains("E11000", StringComparison.Ordinal))
        {
            rejectedDuplicate = true;
        }

        Require(rejectedDuplicate && await rawMetadata.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty) == 1,
            "The unique FileId index did not reject duplicate metadata.");

        var rejectedMissingFile = false;
        try
        {
            await metadata.AddAsync(new ContentFileMetadata { FileId = int.MaxValue, MetadataJson = "{}" }, default);
        }
        catch (InvalidOperationException)
        {
            rejectedMissingFile = true;
        }

        Require(rejectedMissingFile, "Metadata for a missing file was accepted.");

        var legacy = await files.AddAsync(CreateFile("legacy.rfa"), default);
        await rawFiles.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", legacy.Id),
            Builders<BsonDocument>.Update.Set("Metadata", BsonDocument.Parse(json)));
        await ContentFileMetadataMigration.RunAsync(context, metadata);
        await ContentFileMetadataMigration.RunAsync(context, metadata);
        var migrated = await rawFiles.Find(Builders<BsonDocument>.Filter.Eq("_id", legacy.Id)).SingleAsync();
        Require(!migrated.Contains("Metadata"), "Legacy metadata remained embedded after migration.");
        Require((await files.GetByIdAsync(legacy.Id, default))!.MetadataJson is not null,
            "Migrating legacy metadata lost its contents.");
        Require(await rawMetadata.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("FileId", legacy.Id)) == 1,
            "Repeated migration duplicated metadata.");

        // Simulate a process stopping after creating a metadata record but before writing its BSON.
        await rawMetadata.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("FileId", legacy.Id),
            Builders<BsonDocument>.Update.Unset("Metadata"));
        await rawFiles.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", legacy.Id),
            Builders<BsonDocument>.Update.Set("Metadata", BsonDocument.Parse(json)));
        await ContentFileMetadataMigration.RunAsync(context, metadata);
        Require(BsonDocument.Parse((await metadata.GetByFileIdAsync(legacy.Id, default))!.MetadataJson).Equals(BsonDocument.Parse(json)),
            "Resuming migration lost legacy metadata after an interrupted write.");

        stored.MetadataJson = """{"RevitCategory":"Окна","Types":[]}""";
        await metadata.UpdateAsync(stored, default);
        Require(BsonDocument.Parse((await files.GetByIdAsync(withMetadata.Id, default))!.MetadataJson!)["RevitCategory"] == "Окна",
            "Updating a metadata document was not reflected in the file API model.");
        withMetadata.Url = "/updated";
        await files.UpdateAsync(withMetadata, default);
        Require(BsonDocument.Parse((await metadata.GetByFileIdAsync(withMetadata.Id, default))!.MetadataJson)
                .Equals(BsonDocument.Parse(stored.MetadataJson)),
            "Updating file fields overwrote separate metadata.");

        await files.DeleteAsync(withoutMetadata, default);
        await files.DeleteAsync(withMetadata, default);
        await files.DeleteAsync(legacy, default);
        Require(await rawMetadata.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty) == 0,
            "Deleting a file left its metadata behind.");
        Console.WriteLine("PASS: separate metadata IDs, sparse FileId links, unique index, migration/resume, updates and deletion.");
    }

    private static ContentFile CreateFile(string name)
    {
        return new ContentFile
        {
            Url = "pending",
            OriginalFileName = name,
            Extension = ".rfa",
            Role = "primary",
            SizeBytes = 3
        };
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
