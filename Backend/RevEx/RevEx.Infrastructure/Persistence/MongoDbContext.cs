using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Tag = RevEx.Domain.Entities.Tag;
using MongoDB.EntityFrameworkCore.Extensions;
using RevEx.Domain.Entities;

namespace RevEx.Infrastructure.Persistence;

public sealed class MongoDbContext : DbContext
{
    private readonly MongoDbOptions settings;
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, MongoClient> Clients = new();
    public MongoDbContext(IOptions<MongoDbOptions> options) => settings = options.Value;
    protected override void OnConfiguring(DbContextOptionsBuilder options) => options.UseMongoDB(settings.ConnectionString, settings.DatabaseName);
    protected override void OnModelCreating(ModelBuilder model)
    {
        Configure<Category>(model, "Contents");
        Configure<Content>(model, "Category", "ContentStatus", "PreviewFile", "Versions", "Tags");
        Configure<ContentVersion>(model, "Content", "Files");
        model.Entity<ContentVersion>().Property(version => version.Platform).HasConversion<int>();
        model.Entity<ContentVersion>().Property(version => version.PublishStatus).HasConversion<int>();
        // Dynamic metadata is persisted in contentFileMetadata, linked by FileId.
        Configure<ContentFile>(model, "ContentVersion", "MetadataJson");
        Configure<ContentFileMetadata>(model, "MetadataJson");
        Configure<ContentStatus>(model, "Contents");
        Configure<Tag>(model, "TagGroup", "Contents");
        Configure<TagGroup>(model, "Tags");
    }
    private static void Configure<T>(ModelBuilder model, params string[] ignored) where T : class
    {
        var entity = model.Entity<T>();
        entity.ToCollection(CollectionName(typeof(T)));
        entity.HasKey("Id");
        entity.Property<int>("Id").ValueGeneratedNever();
        foreach (var name in ignored) entity.Ignore(name);
    }
    public static string CollectionName(Type type) => type.Name switch
    {
        nameof(Category) => "categories", nameof(Content) => "contents",
        nameof(ContentVersion) => "contentVersions", nameof(ContentFile) => "contentFiles",
        nameof(ContentFileMetadata) => "contentFileMetadata",
        nameof(ContentStatus) => "contentStatuses", nameof(Tag) => "tags", nameof(TagGroup) => "tagGroups",
        _ => throw new NotSupportedException(type.Name)
    };
    // Atomic counters are a provider escape hatch: EF cannot express find-and-increment.
    internal IMongoCollection<TDocument> GetCollection<TDocument>(string name)
    {
        return Clients.GetOrAdd(settings.ConnectionString, value => new MongoClient(value))
            .GetDatabase(settings.DatabaseName)
            .GetCollection<TDocument>(name);
    }

    public async Task<int> NextIdAsync<T>(CancellationToken token)
    {
        var counters = Clients.GetOrAdd(settings.ConnectionString, value => new MongoClient(value)).GetDatabase(settings.DatabaseName).GetCollection<Repositories.Counter>("counters");
        var result = await counters.FindOneAndUpdateAsync(
            Builders<Repositories.Counter>.Filter.Eq(x => x.Id, CollectionName(typeof(T))),
            Builders<Repositories.Counter>.Update.Inc(x => x.Sequence, 1),
            new FindOneAndUpdateOptions<Repositories.Counter> { IsUpsert = true, ReturnDocument = ReturnDocument.After }, token);
        return result.Sequence;
    }
}
