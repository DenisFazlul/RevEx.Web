using RevEx.Domain.Contracts.ContentHistory;

namespace RevEx.Application.Abstractions.Persistence;

public interface IContentHistoryStore
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ContentHistoryEventDto>> GetByContentIdAsync(
        int contentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a history event and atomically queues notifications for its current subscribers.
    /// </summary>
    Task<ContentHistoryEventDto> AddAsync(
        int contentId,
        string description,
        string author,
        CancellationToken cancellationToken = default);
}
