using Microsoft.EntityFrameworkCore;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Domain.Entities;
using RevEx.Infrastructure.Persistence;

namespace RevEx.Infrastructure.Repositories;

public sealed class PostgresContentStatusRepository(PostgresDbContext context)
    : PostgresRepository<ContentStatus>(context), IContentStatusRepository
{
    public Task<ContentStatus?> GetByNameAsync(string name, CancellationToken token)
    {
        return Context.ContentStatuses
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Name == name, token);
    }

    public Task<bool> NameExistsAsync(string name, int? excludedId, CancellationToken token)
    {
        return Context.ContentStatuses
            .AsNoTracking()
            .AnyAsync(
                entity => entity.Id != excludedId && entity.Name == name,
                token);
    }
}
