using RevEx.Application.Abstractions.Persistence;
using RevEx.Domain.Entities;
using RevEx.Infrastructure.Persistence;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Driver;

namespace RevEx.Infrastructure.Repositories;

public sealed class MongoContentFileRepository(MongoDbContext context)
    : MongoRepository<ContentFile>(context, (entity, id) => entity.Id = id), IContentFileRepository
{
    private IMongoCollection<BsonDocument> Documents =>
        Context.GetCollection<BsonDocument>(MongoDbContext.CollectionName(typeof(ContentFile)));

    public override async Task<IReadOnlyCollection<ContentFile>> GetAllAsync(CancellationToken token)
    {
        var files = await base.GetAllAsync(token);
        if (files.Count == 0)
        {
            return files;
        }

        var documents = await Documents.Find(Builders<BsonDocument>.Filter.In("_id", files.Select(file => file.Id)))
            .Project(Builders<BsonDocument>.Projection.Include("_id").Include("Metadata"))
            .ToListAsync(token);
        var metadata = documents.ToDictionary(document => document["_id"].AsInt32, ReadMetadata);
        foreach (var file in files)
        {
            file.MetadataJson = metadata.GetValueOrDefault(file.Id);
        }

        return files;
    }

    public override async Task<ContentFile?> GetByIdAsync(int id, CancellationToken token)
    {
        var file = await base.GetByIdAsync(id, token);
        if (file is not null)
        {
            var document = await Documents.Find(Builders<BsonDocument>.Filter.Eq("_id", id))
                .Project(Builders<BsonDocument>.Projection.Include("Metadata"))
                .FirstOrDefaultAsync(token);
            file.MetadataJson = document is null ? null : ReadMetadata(document);
        }

        return file;
    }

    public override async Task<ContentFile> AddAsync(ContentFile entity, CancellationToken token)
    {
        // Parse before creating the record so invalid metadata cannot leave an orphan file.
        var metadata = entity.MetadataJson is null ? null : BsonDocument.Parse(entity.MetadataJson);
        var result = await base.AddAsync(entity, token);
        if (metadata is not null)
        {
            try
            {
                await Documents.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", entity.Id),
                    Builders<BsonDocument>.Update.Set("Metadata", metadata), cancellationToken: token);
            }
            catch
            {
                await base.DeleteAsync(entity, CancellationToken.None);
                throw;
            }
        }

        return result;
    }

    // EF updates mapped fields with $set, preserving the dynamic Metadata document.
    private static string? ReadMetadata(BsonDocument document)
    {
        if (!document.TryGetValue("Metadata", out var value) || value.IsBsonNull)
        {
            return null;
        }

        return value.ToJson(new JsonWriterSettings { OutputMode = JsonOutputMode.RelaxedExtendedJson });
    }
}
