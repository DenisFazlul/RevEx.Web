using RevEx.Domain.Entities;

namespace RevEx.Application.Abstractions.Persistence;

public interface IContentFileRepository : IRepository<ContentFile>
{
    Task<IReadOnlyCollection<ContentFile>> GetByContentVersionIdAsync(
        int contentVersionId,
        CancellationToken token);
}
