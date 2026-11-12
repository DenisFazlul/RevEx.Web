using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;
using MongoDB.Driver;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Domain.Entities;
using RevEx.Infrastructure.Persistence;
using DomainTag = RevEx.Domain.Entities.Tag;

namespace RevEx.Infrastructure.Repositories;

public sealed class MongoContentRepository(MongoDbContext context)
    : MongoRepository<Content>(context, (entity, id) => entity.Id = id), IContentRepository
{
    private IMongoCollection<BsonDocument> Documents =>
        Context.GetCollection<BsonDocument>(MongoDbContext.CollectionName(typeof(Content)));

    public Task<Content?> GetByCidAsync(Guid cid, CancellationToken token)
    {
        return Context.Set<Content>().AsNoTracking().FirstOrDefaultAsync(entity => entity.Cid == cid, token);
    }

    public override async Task<Content> AddAsync(Content entity, CancellationToken token)
    {
        await EnsureCidIndexAsync(token);
        return await base.AddAsync(entity, token);
    }

    public override async Task<IReadOnlyCollection<Content>> GetAllAsync(CancellationToken token)
    {
        var entities = await base.GetAllAsync(token);
        var ids = entities.SelectMany(entity => entity.TagIds).Distinct().ToArray();
        if (ids.Length > 0)
        {
            var tags = await Context.Set<DomainTag>().AsNoTracking().Where(tag => ids.Contains(tag.Id)).ToListAsync(token);
            var byId = tags.ToDictionary(tag => tag.Id);
            foreach (var entity in entities)
                entity.Tags = entity.TagIds.Where(byId.ContainsKey).Select(id => byId[id]).ToList();
        }
        return entities;
    }

    public override async Task<Content?> GetByIdAsync(int id, CancellationToken token)
    {
        var entity = await base.GetByIdAsync(id, token);
        if (entity is not null && entity.TagIds.Count > 0)
        {
            var ids = entity.TagIds.ToArray();
            entity.Tags = await Context.Set<DomainTag>().AsNoTracking().Where(tag => ids.Contains(tag.Id)).ToListAsync(token);
        }
        return entity;
    }

    private Task EnsureCidIndexAsync(CancellationToken token)
    {
        var options = new CreateIndexOptions<BsonDocument>
        {
            Unique = true,
            Name = "UX_contents_Cid",
            PartialFilterExpression = new BsonDocument("Cid", new BsonDocument("$type", "binData"))
        };
        var model = new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("Cid"),
            options);
        return Documents.Indexes.CreateOneAsync(model, cancellationToken: token);
    }
}
