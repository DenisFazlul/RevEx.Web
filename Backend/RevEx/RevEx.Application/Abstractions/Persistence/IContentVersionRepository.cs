using RevEx.Domain.Entities;

namespace RevEx.Application.Abstractions.Persistence;

public interface IContentVersionRepository : IRepository<ContentVersion>
{
    Task<ContentVersion?> GetByVidAsync(Guid vid, CancellationToken token);

    Task<IReadOnlyCollection<ContentVersion>> GetByContentIdAsync(
        int contentId,
        CancellationToken token);

    Task<ContentVersion?> GetByContentIdAndNameAsync(
        int contentId,
        string name,
        CancellationToken token);
}
