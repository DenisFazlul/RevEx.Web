using Microsoft.EntityFrameworkCore;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Domain.Entities;
using RevEx.Infrastructure.Persistence;

namespace RevEx.Infrastructure.Repositories;

public sealed class PostgresTagGroupRepository(PostgresDbContext context)
    : PostgresRepository<TagGroup>(context), ITagGroupRepository
{
    public Task<TagGroup?> GetByNameAsync(string name, CancellationToken token)
    {
        return Context.TagGroups
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Name == name, token);
    }

    public Task<bool> NameExistsAsync(string name, int? excludedId, CancellationToken token)
    {
        return Context.TagGroups
            .AsNoTracking()
            .AnyAsync(
                entity => entity.Id != excludedId && entity.Name == name,
                token);
    }
}
