using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;
using MongoDB.Driver;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Domain.Entities;
using RevEx.Infrastructure.Persistence;

namespace RevEx.Infrastructure.Repositories;

public sealed class MongoContentVersionRepository(MongoDbContext context)
    : MongoRepository<ContentVersion>(context, (entity, id) => entity.Id = id), IContentVersionRepository
{
    private IMongoCollection<BsonDocument> Documents =>
        Context.GetCollection<BsonDocument>(MongoDbContext.CollectionName(typeof(ContentVersion)));

    public Task<ContentVersion?> GetByVidAsync(Guid vid, CancellationToken token)
    {
        return Context.Set<ContentVersion>().AsNoTracking().FirstOrDefaultAsync(entity => entity.Vid == vid, token);
    }

    public override async Task<ContentVersion> AddAsync(ContentVersion entity, CancellationToken token)
    {
        await EnsureVidIndexAsync(token);
        return await base.AddAsync(entity, token);
    }

    public override async Task<IReadOnlyCollection<ContentVersion>> GetAllAsync(CancellationToken token)
    {
        var entities = await base.GetAllAsync(token);
        var ids = entities.Select(entity => (int?)entity.Id).ToArray();
        if (ids.Length > 0)
        {
            var files = await Context.Set<ContentFile>().AsNoTracking().Where(file => ids.Contains(file.ContentVersionId)).ToListAsync(token);
            var byVersion = files.ToLookup(file => file.ContentVersionId);
            foreach (var entity in entities)
                entity.Files = byVersion[entity.Id].ToList();
        }
        return entities;
    }

    public override async Task<ContentVersion?> GetByIdAsync(int id, CancellationToken token)
    {
        var entity = await base.GetByIdAsync(id, token);
        if (entity is not null)
            entity.Files = await Context.Set<ContentFile>().AsNoTracking().Where(file => file.ContentVersionId == entity.Id).ToListAsync(token);
        return entity;
    }

    private Task EnsureVidIndexAsync(CancellationToken token)
    {
        var options = new CreateIndexOptions<BsonDocument>
        {
            Unique = true,
            Name = "UX_contentVersions_Vid",
            PartialFilterExpression = new BsonDocument("Vid", new BsonDocument("$type", "binData"))
        };
        var model = new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("Vid"),
            options);
        return Documents.Indexes.CreateOneAsync(model, cancellationToken: token);
    }
}
