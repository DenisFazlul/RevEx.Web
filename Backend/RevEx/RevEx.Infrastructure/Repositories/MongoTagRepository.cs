using Microsoft.EntityFrameworkCore;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Domain.Entities;
using RevEx.Infrastructure.Persistence;

namespace RevEx.Infrastructure.Repositories;

public sealed class MongoTagRepository(MongoDbContext context)
    : MongoRepository<Tag>(context, (entity, id) => entity.Id = id), ITagRepository
{
    public override async Task<IReadOnlyCollection<Tag>> GetAllAsync(CancellationToken token)
    {
        var entities = await base.GetAllAsync(token);
        var ids = entities.Select(entity => entity.TagGroupId).Distinct().ToArray();
        if (ids.Length > 0)
        {
            var groups = await Context.Set<TagGroup>().AsNoTracking().Where(group => ids.Contains(group.Id)).ToDictionaryAsync(group => group.Id, token);
            foreach (var entity in entities)
                entity.TagGroup = groups.GetValueOrDefault(entity.TagGroupId);
        }
        return entities;
    }

    public override async Task<Tag?> GetByIdAsync(int id, CancellationToken token)
    {
        var entity = await base.GetByIdAsync(id, token);
        if (entity is not null)
            entity.TagGroup = await Context.Set<TagGroup>().AsNoTracking().FirstOrDefaultAsync(group => group.Id == entity.TagGroupId, token);
        return entity;
    }
}
