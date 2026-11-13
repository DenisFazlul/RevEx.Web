using Microsoft.EntityFrameworkCore;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Domain.Entities;
using RevEx.Infrastructure.Persistence;

namespace RevEx.Infrastructure.Repositories;

public sealed class PostgresTagRepository(PostgresDbContext context)
    : PostgresRepository<Tag>(context), ITagRepository
{
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
