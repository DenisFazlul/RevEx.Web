using Microsoft.EntityFrameworkCore;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Domain.Entities;
using RevEx.Infrastructure.Persistence;

namespace RevEx.Infrastructure.Repositories;

public sealed class PostgresCategoryRepository(PostgresDbContext context)
    : PostgresRepository<Category>(context), ICategoryRepository
{
    public Task<Category?> GetByNameAsync(string name, CancellationToken token)
    {
        return Context.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Name == name, token);
    }
}
