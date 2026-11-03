using RevEx.Application.Abstractions.Persistence;
using RevEx.Domain.Entities;
using RevEx.Infrastructure.Persistence;

namespace RevEx.Infrastructure.Repositories;

public sealed class MongoContentStatusRepository(MongoDbContext context)
    : MongoRepository<ContentStatus>(context, (entity, id) => entity.Id = id), IContentStatusRepository
{
}
