using MongoDB.Bson;
using MongoDB.Driver;
using RevEx.Domain.Entities;

namespace RevEx.Infrastructure.Persistence.Migrations;

/// <summary>Заполняет платформу и статус публикации старых версий, сохраняя уже заданные значения.</summary>
public static class ContentVersionEnumsMigration
{
    public static async Task RunAsync(MongoDbContext context, CancellationToken token = default)
    {
        var versions = context.GetCollection<BsonDocument>(MongoDbContext.CollectionName(typeof(ContentVersion)));
        var filter = Builders<BsonDocument>.Filter;
        var missingPlatform = filter.Exists("Platform", false) | filter.Eq("Platform", BsonNull.Value);
        foreach (var platform in new[] { Platform.Revit, Platform.AutoCad })
        {
            var application = filter.Regex("Application", new BsonRegularExpression($"^\\s*{platform}\\s*$", "i"));
            await versions.UpdateManyAsync(missingPlatform & application,
                Builders<BsonDocument>.Update.Set("Platform", (int)platform), cancellationToken: token);
        }

        // Versions created before Application was stored used Revit by default.
        await versions.UpdateManyAsync(missingPlatform & (filter.Exists("Application", false) | filter.Eq("Application", BsonNull.Value)),
            Builders<BsonDocument>.Update.Set("Platform", (int)Platform.Revit), cancellationToken: token);
        await versions.UpdateManyAsync(missingPlatform,
            Builders<BsonDocument>.Update.Set("Platform", (int)Platform.Other), cancellationToken: token);

        var missingStatus = filter.Exists("PublishStatus", false) | filter.Eq("PublishStatus", BsonNull.Value);
        foreach (var entry in new[] { ("published", PublishStatus.Ready), ("archived", PublishStatus.Withdrawn) })
        {
            var status = filter.Regex("Status", new BsonRegularExpression($"^\\s*{entry.Item1}\\s*$", "i"));
            await versions.UpdateManyAsync(missingStatus & status,
                Builders<BsonDocument>.Update.Set("PublishStatus", (int)entry.Item2), cancellationToken: token);
        }

        await versions.UpdateManyAsync(missingStatus,
            Builders<BsonDocument>.Update.Set("PublishStatus", (int)PublishStatus.Draft), cancellationToken: token);
    }
}
