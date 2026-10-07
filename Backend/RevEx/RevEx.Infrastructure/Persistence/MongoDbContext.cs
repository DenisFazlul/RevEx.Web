using Microsoft.Extensions.Options;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using RevEx.Domain.Entities;
using DomainTag = RevEx.Domain.Entities.Tag;

namespace RevEx.Infrastructure.Persistence;

public sealed class MongoDbContext
{
    private static readonly object MappingLock = new();
    private static bool _mappingConfigured;

    public MongoDbContext(IOptions<MongoDbOptions> options)
    {
        ConfigureMappings();
        var settings = options.Value;
        Database = new MongoClient(settings.ConnectionString).GetDatabase(settings.DatabaseName);
    }

    public IMongoDatabase Database { get; }

    public IMongoCollection<TEntity> Collection<TEntity>() where TEntity : class =>
        Database.GetCollection<TEntity>(CollectionName(typeof(TEntity)));

    public static string CollectionName(Type type)
    {
        if (type == typeof(Category)) return "categories";
        if (type == typeof(Content)) return "contents";
        if (type == typeof(ContentVersion)) return "contentVersions";
        if (type == typeof(ContentFile)) return "contentFiles";
        if (type == typeof(ContentStatus)) return "contentStatuses";
        if (type == typeof(DomainTag)) return "tags";
        if (type == typeof(TagGroup)) return "tagGroups";
        throw new NotSupportedException($"MongoDB collection for {type.Name} is not configured.");
    }

    private static void ConfigureMappings()
    {
        lock (MappingLock)
        {
            if (_mappingConfigured) return;
            Map<Category>(map => map.UnmapMember(x => x.Contents));
            Map<Content>(map =>
            {
                map.UnmapMember(x => x.Category);
                map.UnmapMember(x => x.ContentStatus);
                map.UnmapMember(x => x.PreviewFile);
                map.UnmapMember(x => x.Versions);
                map.UnmapMember(x => x.Tags);
            });
            Map<ContentVersion>(map =>
            {
                map.UnmapMember(x => x.Content);
                map.UnmapMember(x => x.Files);
            });
            Map<ContentFile>(map => map.UnmapMember(x => x.ContentVersion));
            Map<ContentStatus>(map => map.UnmapMember(x => x.Contents));
            Map<DomainTag>(map =>
            {
                map.UnmapMember(x => x.TagGroup);
                map.UnmapMember(x => x.Contents);
            });
            Map<TagGroup>(map => map.UnmapMember(x => x.Tags));
            _mappingConfigured = true;
        }
    }

    private static void Map<TEntity>(Action<BsonClassMap<TEntity>> configure)
    {
        if (BsonClassMap.IsClassMapRegistered(typeof(TEntity))) return;
        BsonClassMap.RegisterClassMap<TEntity>(map =>
        {
            map.AutoMap();
            map.MapIdMember(typeof(TEntity).GetProperty("Id")!);
            configure(map);
        });
    }
}
