using Microsoft.EntityFrameworkCore;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Domain.Entities;
using RevEx.Infrastructure.Persistence;

namespace RevEx.Infrastructure.Repositories;

public sealed class MongoContentRepository(MongoDbContext context)
    : MongoRepository<Content>(context, (entity, id) => entity.Id = id), IContentRepository
{
    public override async Task<IReadOnlyCollection<Content>> GetAllAsync(CancellationToken token)
    {
        var entities = await base.GetAllAsync(token);
        var ids = entities.SelectMany(entity => entity.TagIds).Distinct().ToArray();
        if (ids.Length > 0)
        {
            var tags = await Context.Set<Tag>().AsNoTracking().Where(tag => ids.Contains(tag.Id)).ToListAsync(token);
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
            entity.Tags = await Context.Set<Tag>().AsNoTracking().Where(tag => ids.Contains(tag.Id)).ToListAsync(token);
        }
        return entity;
    }
}
