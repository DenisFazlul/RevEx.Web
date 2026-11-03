using RevEx.Application.Abstractions.Persistence;
using RevEx.Domain.Entities;
using RevEx.Infrastructure.Persistence;

namespace RevEx.Infrastructure.Repositories;

public sealed class MongoCategoryRepository(MongoDbContext context)
    : MongoRepository<Category>(context, (entity, id) => entity.Id = id), ICategoryRepository
{
}
