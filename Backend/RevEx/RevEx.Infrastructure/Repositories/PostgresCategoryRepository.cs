using RevEx.Application.Abstractions.Persistence;
using RevEx.Domain.Entities;
using RevEx.Infrastructure.Persistence;

namespace RevEx.Infrastructure.Repositories;

public sealed class PostgresCategoryRepository(PostgresDbContext context)
    : PostgresRepository<Category>(context), ICategoryRepository
{
}
