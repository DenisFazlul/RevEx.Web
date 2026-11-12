using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Driver;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Domain.Entities;
using RevEx.Infrastructure.Persistence;

namespace RevEx.Infrastructure.Repositories;

public sealed class MongoContentFileMetadataRepository(MongoDbContext context)
    : MongoRepository<ContentFileMetadata>(context, (entity, id) => entity.Id = id), IContentFileMetadataRepository
{
    private IMongoCollection<BsonDocument> Documents =>
        Context.GetCollection<BsonDocument>(MongoDbContext.CollectionName(typeof(ContentFileMetadata)));

    public override async Task<IReadOnlyCollection<ContentFileMetadata>> GetAllAsync(CancellationToken token)
    {
        var metadata = await base.GetAllAsync(token);
        await LoadMetadataAsync(metadata, token);
        return metadata;
    }

    public override async Task<ContentFileMetadata?> GetByIdAsync(int id, CancellationToken token)
    {
        var metadata = await base.GetByIdAsync(id, token);
        if (metadata is not null)
        {
            await LoadMetadataAsync([metadata], token);
        }

        return metadata;
    }

    public async Task<ContentFileMetadata?> GetByFileIdAsync(int fileId, CancellationToken token)
    {
        var metadata = await GetByFileIdsAsync([fileId], token);
        return metadata.SingleOrDefault();
    }

    public async Task<IReadOnlyCollection<ContentFileMetadata>> GetByFileIdsAsync(
        IReadOnlyCollection<int> fileIds,
        CancellationToken token)
    {
        if (fileIds.Count == 0)
        {
            return [];
        }

        var metadata = await Context.Set<ContentFileMetadata>().AsNoTracking()
            .Where(entity => fileIds.Contains(entity.FileId))
            .ToListAsync(token);
        await LoadMetadataAsync(metadata, token);
        return metadata;
    }

    public override async Task<ContentFileMetadata> AddAsync(ContentFileMetadata entity, CancellationToken token)
    {
        var document = BsonDocument.Parse(entity.MetadataJson);
        var fileExists = await Context.Set<ContentFile>().AsNoTracking().AnyAsync(file => file.Id == entity.FileId, token);
        if (!fileExists)
        {
            throw new InvalidOperationException("Cannot create metadata for a file that does not exist.");
        }

        await Documents.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("FileId"),
            new CreateIndexOptions { Unique = true, Name = "UX_contentFileMetadata_FileId" }), cancellationToken: token);

        ContentFileMetadata result;
        try
        {
            result = await base.AddAsync(entity, token);
        }
        catch
        {
            Context.ChangeTracker.Clear();
            throw;
        }

        try
        {
            await Documents.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", entity.Id),
                Builders<BsonDocument>.Update.Set("Metadata", document), cancellationToken: token);
        }
        catch
        {
            await base.DeleteAsync(entity, CancellationToken.None);
            throw;
        }

        return result;
    }

    public override async Task UpdateAsync(ContentFileMetadata entity, CancellationToken token)
    {
        var document = BsonDocument.Parse(entity.MetadataJson);
        var existing = await base.GetByIdAsync(entity.Id, token)
            ?? throw new InvalidOperationException("File metadata was not found.");
        if (existing.FileId != entity.FileId)
        {
            throw new InvalidOperationException("File metadata cannot be assigned to another file.");
        }

        await base.UpdateAsync(entity, token);
        await Documents.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", entity.Id),
            Builders<BsonDocument>.Update.Set("Metadata", document), cancellationToken: token);
    }

    private async Task LoadMetadataAsync(IReadOnlyCollection<ContentFileMetadata> metadata, CancellationToken token)
    {
        if (metadata.Count == 0)
        {
            return;
        }

        var documents = await Documents.Find(Builders<BsonDocument>.Filter.In("_id", metadata.Select(entity => entity.Id)))
            .Project(Builders<BsonDocument>.Projection.Include("_id").Include("Metadata"))
            .ToListAsync(token);
        var byId = documents.ToDictionary(document => document["_id"].AsInt32);
        foreach (var entity in metadata)
        {
            if (byId.TryGetValue(entity.Id, out var document)
                && document.TryGetValue("Metadata", out var value) && value.IsBsonDocument)
            {
                entity.MetadataJson = value.ToJson(new JsonWriterSettings { OutputMode = JsonOutputMode.RelaxedExtendedJson });
            }
        }
    }
}
