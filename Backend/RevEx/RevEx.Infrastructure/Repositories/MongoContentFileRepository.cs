using RevEx.Application.Abstractions.Persistence;
using RevEx.Domain.Entities;
using RevEx.Infrastructure.Persistence;

namespace RevEx.Infrastructure.Repositories;

public sealed class MongoContentFileRepository(MongoDbContext context)
    : MongoRepository<ContentFile>(context, (entity, id) => entity.Id = id), IContentFileRepository
{
}
