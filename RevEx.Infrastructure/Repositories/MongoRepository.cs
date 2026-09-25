using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Domain.Entities;
using RevEx.Infrastructure.Persistence;
using ContentEntity = RevEx.Domain.Entities.Content;
using DomainTag = RevEx.Domain.Entities.Tag;

namespace RevEx.Infrastructure.Repositories;

public sealed class MongoRepository<TEntity>(MongoDbContext context) : IRepository<TEntity>
    where TEntity : class
{
    private readonly IMongoCollection<TEntity> _collection = context.Collection<TEntity>();

    public async Task<IReadOnlyCollection<TEntity>> GetAllAsync(CancellationToken cancellationToken)
    {
        var entities = await _collection.Find(FilterDefinition<TEntity>.Empty).ToListAsync(cancellationToken);
        foreach (var entity in entities) await HydrateAsync(entity, cancellationToken);
        return entities;
    }

    public async Task<TEntity?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var entity = await _collection.Find(Builders<TEntity>.Filter.Eq("_id", id))
            .FirstOrDefaultAsync(cancellationToken);
        if (entity is not null) await HydrateAsync(entity, cancellationToken);
        return entity;
    }

    public async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken)
    {
        SetId(entity, await NextIdAsync(cancellationToken));
        SetAudit(entity, true);
        Normalize(entity);
        await _collection.InsertOneAsync(entity, cancellationToken: cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(TEntity entity, CancellationToken cancellationToken)
    {
        SetAudit(entity, false);
        Normalize(entity);
        await _collection.ReplaceOneAsync(Builders<TEntity>.Filter.Eq("_id", GetId(entity)), entity,
            cancellationToken: cancellationToken);
    }

    public async Task DeleteAsync(TEntity entity, CancellationToken cancellationToken)
    {
        var id = GetId(entity);
        await _collection.DeleteOneAsync(Builders<TEntity>.Filter.Eq("_id", id), cancellationToken);
        if (entity is DomainTag)
        {
            await context.Collection<ContentEntity>().UpdateManyAsync(
                Builders<ContentEntity>.Filter.AnyEq(x => x.TagIds, id),
                Builders<ContentEntity>.Update.Pull(x => x.TagIds, id),
                cancellationToken: cancellationToken);
        }
        else if (entity is ContentVersion)
        {
            await context.Collection<ContentFile>().DeleteManyAsync(x => x.ContentVersionId == id, cancellationToken);
        }
        else if (entity is ContentEntity)
        {
            var versions = await context.Collection<ContentVersion>().Find(x => x.ContentId == id)
                .Project(x => x.Id).ToListAsync(cancellationToken);
            await context.Collection<ContentVersion>().DeleteManyAsync(x => x.ContentId == id, cancellationToken);
            if (versions.Count > 0)
                await context.Collection<ContentFile>().DeleteManyAsync(
                    Builders<ContentFile>.Filter.In(x => x.ContentVersionId, versions.Cast<int?>()), cancellationToken);
        }
    }

    private async Task<int> NextIdAsync(CancellationToken cancellationToken)
    {
        var counters = context.Database.GetCollection<Counter>("counters");
        var name = MongoDbContext.CollectionName(typeof(TEntity));
        var counter = await counters.FindOneAndUpdateAsync(
            Builders<Counter>.Filter.Eq(x => x.Id, name),
            Builders<Counter>.Update.Inc(x => x.Sequence, 1),
            new FindOneAndUpdateOptions<Counter> { IsUpsert = true, ReturnDocument = ReturnDocument.After },
            cancellationToken);
        return counter.Sequence;
    }

    private static void Normalize(TEntity entity)
    {
        if (entity is ContentEntity content && content.Tags.Count > 0)
            content.TagIds = content.Tags.Select(x => x.Id).Distinct().ToArray();
    }

    private async Task HydrateAsync(TEntity entity, CancellationToken cancellationToken)
    {
        switch (entity)
        {
            case ContentEntity content:
                content.Tags = content.TagIds.Count == 0 ? [] :
                    await context.Collection<DomainTag>().Find(Builders<DomainTag>.Filter.In(x => x.Id, content.TagIds))
                        .ToListAsync(cancellationToken);
                break;
            case DomainTag tag:
                tag.TagGroup = await context.Collection<TagGroup>().Find(x => x.Id == tag.TagGroupId)
                    .FirstOrDefaultAsync(cancellationToken);
                break;
            case ContentVersion version:
                version.Files = await context.Collection<ContentFile>().Find(x => x.ContentVersionId == version.Id)
                    .ToListAsync(cancellationToken);
                break;
        }
    }

    private static int GetId(TEntity entity) => (int)(typeof(TEntity).GetProperty("Id")?.GetValue(entity) ?? 0);
    private static void SetId(TEntity entity, int id) => typeof(TEntity).GetProperty("Id")!.SetValue(entity, id);

    private static void SetAudit(TEntity entity, bool created)
    {
        if (entity is not AuditableEntity auditable) return;
        var now = DateTimeOffset.UtcNow;
        if (created) auditable.CreatedAt = now;
        auditable.UpdatedAt = now;
    }

    private sealed class Counter
    {
        [BsonId] public required string Id { get; init; }
        public int Sequence { get; init; }
    }
}
