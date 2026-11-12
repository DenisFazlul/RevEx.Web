using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Driver;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Domain.Entities;

namespace RevEx.Infrastructure.Persistence.Migrations;

/// <summary>Переносит прежние вложенные метаданные в отдельные документы до запуска API.</summary>
public static class ContentFileMetadataMigration
{
    public static async Task RunAsync(
        MongoDbContext context,
        IContentFileMetadataRepository metadataRepository,
        CancellationToken token = default)
    {
        var files = context.GetCollection<BsonDocument>(MongoDbContext.CollectionName(typeof(ContentFile)));
        var filter = Builders<BsonDocument>.Filter.Type("Metadata", BsonType.Document);
        using var cursor = await files.Find(filter)
            .Project(Builders<BsonDocument>.Projection.Include("_id").Include("Metadata"))
            .ToCursorAsync(token);
        while (await cursor.MoveNextAsync(token))
        {
            foreach (var file in cursor.Current)
            {
                var fileId = file["_id"].AsInt32;
                var metadata = await metadataRepository.GetByFileIdAsync(fileId, token);
                if (metadata is null)
                {
                    await metadataRepository.AddAsync(new ContentFileMetadata
                    {
                        FileId = fileId,
                        MetadataJson = file["Metadata"].ToJson(new JsonWriterSettings { OutputMode = JsonOutputMode.RelaxedExtendedJson })
                    }, token);
                }
                else if (string.IsNullOrEmpty(metadata.MetadataJson))
                {
                    // Resume if a previous process stopped between creating the record and writing BSON.
                    metadata.MetadataJson = file["Metadata"].ToJson(new JsonWriterSettings { OutputMode = JsonOutputMode.RelaxedExtendedJson });
                    await metadataRepository.UpdateAsync(metadata, token);
                }

                // Remove the legacy field only after a separate metadata document exists.
                var unchanged = Builders<BsonDocument>.Filter.Eq("_id", fileId)
                    & Builders<BsonDocument>.Filter.Eq("Metadata", file["Metadata"]);
                await files.UpdateOneAsync(unchanged, Builders<BsonDocument>.Update.Unset("Metadata"), cancellationToken: token);
            }
        }
    }
}
