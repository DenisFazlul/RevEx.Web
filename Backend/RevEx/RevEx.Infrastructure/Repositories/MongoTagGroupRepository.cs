using RevEx.Application.Abstractions.Persistence;
using RevEx.Domain.Entities;
using RevEx.Infrastructure.Persistence;

namespace RevEx.Infrastructure.Repositories;

public sealed class MongoTagGroupRepository(MongoDbContext context)
    : MongoRepository<TagGroup>(context, (entity, id) => entity.Id = id), ITagGroupRepository
{
}
