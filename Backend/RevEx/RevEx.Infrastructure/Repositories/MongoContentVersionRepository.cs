using Microsoft.EntityFrameworkCore;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Domain.Entities;
using RevEx.Infrastructure.Persistence;

namespace RevEx.Infrastructure.Repositories;

public sealed class MongoContentVersionRepository(MongoDbContext context)
    : MongoRepository<ContentVersion>(context, (entity, id) => entity.Id = id), IContentVersionRepository
{
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
}
