using RevEx.Domain.Contracts.ContentHistory;

namespace RevEx.Application.Abstractions.Persistence;

public interface IContentHistoryStore
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ContentHistoryEventDto>> GetByContentIdAsync(
        int contentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a history event and publishes the change to Kafka.
    /// </summary>
    Task<ContentHistoryEventDto> AddAsync(
        int contentId,
        string description,
        string author,
        CancellationToken cancellationToken = default);
}
