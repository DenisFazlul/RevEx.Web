using ContentEntity = RevEx.Domain.Entities.Content;

namespace RevEx.Application.Abstractions.Persistence;

public interface IContentRepository : IRepository<ContentEntity>
{
}
