using Microsoft.EntityFrameworkCore;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Domain.Entities;
using RevEx.Infrastructure.Persistence;

namespace RevEx.Infrastructure.Repositories;

public sealed class PostgresContentParameterRepository(PostgresDbContext context) : PostgresRepository<ContentParameter>(context), IContentParameterRepository
{
    public async Task<IReadOnlyCollection<ContentParameter>> ListAsync(int contentId, CancellationToken token)
    {
        return await Context.Set<ContentParameter>().AsNoTracking()
            .Where(parameter => parameter.ContentId == contentId)
            .OrderBy(parameter => parameter.Id)
            .ToArrayAsync(token);
    }

    public Task<ContentParameter?> FindAsync(int contentId, int id, CancellationToken token)
    {
        return Context.Set<ContentParameter>().AsNoTracking()
            .SingleOrDefaultAsync(parameter => parameter.ContentId == contentId && parameter.Id == id, token);
    }
}
