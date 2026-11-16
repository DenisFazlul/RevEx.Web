using Microsoft.EntityFrameworkCore;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Domain.Entities;
using RevEx.Infrastructure.Persistence;

namespace RevEx.Infrastructure.Repositories;

public sealed class PostgresTagRepository(PostgresDbContext context)
    : PostgresRepository<Tag>(context), ITagRepository
{
    public async Task<IReadOnlyCollection<Tag>> GetByIdsAsync(
        IReadOnlyCollection<int> ids,
        CancellationToken token)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var entities = await Context.Tags
            .AsNoTracking()
            .Where(entity => ids.Contains(entity.Id))
            .ToListAsync(token);
        return entities;
    }

    public Task<bool> AnyInGroupAsync(int tagGroupId, CancellationToken token)
    {
        return Context.Tags
            .AsNoTracking()
            .AnyAsync(entity => entity.TagGroupId == tagGroupId, token);
    }

    public Task<bool> NameExistsAsync(string name, int? excludedId, CancellationToken token)
    {
        return Context.Tags
            .AsNoTracking()
            .AnyAsync(
                entity => entity.Id != excludedId && entity.Name == name,
                token);
    }

    public override async Task<IReadOnlyCollection<Tag>> GetAllAsync(CancellationToken token)
    {
        var entities = await Context.Tags
            .AsNoTracking()
            .Include(entity => entity.TagGroup)
            .ToListAsync(token);
        return entities;
    }

    public override Task<Tag?> GetByIdAsync(int id, CancellationToken token)
    {
        return Context.Tags
            .AsNoTracking()
            .Include(entity => entity.TagGroup)
            .FirstOrDefaultAsync(entity => entity.Id == id, token);
    }
}
