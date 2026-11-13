using Microsoft.EntityFrameworkCore;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Domain.Entities;
using RevEx.Infrastructure.Persistence;

namespace RevEx.Infrastructure.Repositories;

public sealed class PostgresContentVersionRepository(PostgresDbContext context)
    : PostgresRepository<ContentVersion>(context), IContentVersionRepository
{
    public Task<ContentVersion?> GetByVidAsync(Guid vid, CancellationToken token)
    {
        return Query().FirstOrDefaultAsync(entity => entity.Vid == vid, token);
    }

    public override async Task<IReadOnlyCollection<ContentVersion>> GetAllAsync(CancellationToken token)
    {
        var entities = await Query().ToListAsync(token);
        return entities;
    }

    public override Task<ContentVersion?> GetByIdAsync(int id, CancellationToken token)
    {
        return Query().FirstOrDefaultAsync(entity => entity.Id == id, token);
    }

    private IQueryable<ContentVersion> Query()
    {
        return Context.ContentVersions
            .AsNoTracking()
            .Include(entity => entity.Files);
    }
}
