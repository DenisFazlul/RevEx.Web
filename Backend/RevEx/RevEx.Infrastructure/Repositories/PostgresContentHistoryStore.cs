using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Domain.Contracts.ContentHistory;
using RevEx.Infrastructure.Messaging;
using RevEx.Infrastructure.Persistence;

namespace RevEx.Infrastructure.Repositories;

public sealed class PostgresContentHistoryStore : IContentHistoryStore, IAsyncDisposable
{
    private readonly PostgresContextFactory factory;
    private readonly ContentChangePublisher publisher;

    public PostgresContentHistoryStore(IOptions<DatabaseOptions> options, ContentChangePublisher publisher)
    {
        factory = new PostgresContextFactory(options);
        this.publisher = publisher;
    }

    public async Task<IReadOnlyCollection<ContentHistoryEventDto>> GetByContentIdAsync(
        int contentId,
        CancellationToken cancellationToken = default)
    {
        await using var database = factory.Create();
        var events = await database.History
            .AsNoTracking()
            .Where(item => item.ContentId == contentId)
            .OrderByDescending(item => item.OccurredAt)
            .ThenByDescending(item => item.Id)
            .Select(item => new ContentHistoryEventDto(
                item.Id,
                item.ContentId,
                item.OccurredAt,
                item.Description,
                item.Author))
            .ToListAsync(cancellationToken);
        return events;
    }

    public async Task<ContentHistoryEventDto> AddAsync(
        int contentId,
        string description,
        string author,
        CancellationToken cancellationToken = default)
    {
        await using var database = factory.Create();
        var entity = new ContentHistoryEvent
        {
            ContentId = contentId,
            Description = description,
            Author = author
        };
        database.History.Add(entity);
        await database.SaveChangesAsync(cancellationToken);

        var change = new ContentHistoryEventDto(entity.Id, contentId, entity.OccurredAt, description, author);
        await publisher.PublishAsync(change, cancellationToken);
        return change;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
