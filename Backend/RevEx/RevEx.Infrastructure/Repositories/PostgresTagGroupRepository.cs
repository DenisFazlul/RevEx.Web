using RevEx.Application.Abstractions.Persistence;
using RevEx.Domain.Entities;
using RevEx.Infrastructure.Persistence;

namespace RevEx.Infrastructure.Repositories;

public sealed class PostgresTagGroupRepository(PostgresDbContext context)
    : PostgresRepository<TagGroup>(context), ITagGroupRepository
{
}
