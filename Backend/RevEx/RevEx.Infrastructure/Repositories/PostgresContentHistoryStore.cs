using RevEx.Infrastructure.Messaging;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Domain.Contracts.ContentHistory;

using RevEx.Infrastructure.Persistence;
namespace RevEx.Infrastructure.Repositories;

public sealed class PostgresContentHistoryStore : IContentHistoryStore, IAsyncDisposable
{
    private readonly PostgresContextFactory factory;
    private readonly ContentChangePublisher _publisher;
    public PostgresContentHistoryStore(IOptions<ContentHistoryOptions> options, ContentChangePublisher publisher)
    {
        factory = new(options);
        _publisher = publisher;
    }
    public async Task<IReadOnlyCollection<ContentHistoryEventDto>> GetByContentIdAsync(
        int contentId,
        CancellationToken cancellationToken = default)
    {
        await using var db = factory.Create();
        return await db.History.AsNoTracking().Where(x => x.ContentId == contentId)
            .OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id)
            .Select(x => new ContentHistoryEventDto(x.Id, x.ContentId, x.OccurredAt, x.Description, x.Author))
            .ToListAsync(cancellationToken);
    }
    public async Task<ContentHistoryEventDto> AddAsync(int contentId, string description, string author, CancellationToken cancellationToken = default)
    {
        await using var db = factory.Create();
        var entity = new ContentHistoryEvent { ContentId = contentId, Description = description, Author = author };
        db.History.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        var change = new ContentHistoryEventDto(entity.Id, contentId, entity.OccurredAt, description, author);
        await _publisher.PublishAsync(change, cancellationToken);
        return change;
    }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
