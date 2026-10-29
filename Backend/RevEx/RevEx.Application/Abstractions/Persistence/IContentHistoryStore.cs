using RevEx.Domain.Contracts.ContentHistory;

namespace RevEx.Application.Abstractions.Persistence;

public interface IContentHistoryStore
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ContentHistoryEventDto>> GetByContentIdAsync(
        int contentId,
        CancellationToken cancellationToken = default);

    Task<ContentHistoryEventDto> AddAsync(
        int contentId,
        string description,
        string author,
        CancellationToken cancellationToken = default);
}
