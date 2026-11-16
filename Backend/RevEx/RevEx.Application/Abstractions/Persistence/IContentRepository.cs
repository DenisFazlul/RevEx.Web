using ContentEntity = RevEx.Domain.Entities.Content;

namespace RevEx.Application.Abstractions.Persistence;

public interface IContentRepository : IRepository<ContentEntity>
{
    Task<ContentEntity?> GetByCidAsync(Guid cid, CancellationToken token);

    Task<bool> AnyWithCategoryIdAsync(int categoryId, CancellationToken token);

    Task<bool> AnyWithStatusIdAsync(int statusId, CancellationToken token);

    Task RemoveTagFromAllAsync(int tagId, CancellationToken token);
}
