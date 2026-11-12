using RevEx.Domain.Entities;

namespace RevEx.Application.Abstractions.Persistence;

public interface IContentFileMetadataRepository : IRepository<ContentFileMetadata>
{
    Task<ContentFileMetadata?> GetByFileIdAsync(int fileId, CancellationToken token);

    Task<IReadOnlyCollection<ContentFileMetadata>> GetByFileIdsAsync(
        IReadOnlyCollection<int> fileIds,
        CancellationToken token);
}
