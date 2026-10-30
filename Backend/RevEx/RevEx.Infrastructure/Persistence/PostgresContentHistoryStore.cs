using Microsoft.Extensions.Options;
using Npgsql;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Domain.Contracts.ContentHistory;

namespace RevEx.Infrastructure.Persistence;

public sealed class PostgresContentHistoryStore : IContentHistoryStore, IAsyncDisposable
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ContentChangePublisher _publisher;

    public PostgresContentHistoryStore(IOptions<ContentHistoryOptions> options, ContentChangePublisher publisher)
    {
        _publisher = publisher;
        var connectionString = options.Value.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("ContentHistory:ConnectionString must be configured.");
        }

        _dataSource = NpgsqlDataSource.Create(connectionString);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS content_history_events (
                id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                content_id integer NOT NULL,
                occurred_at timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                description text NOT NULL,
                author varchar(300) NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_content_history_events_content_time
                ON content_history_events (content_id, occurred_at DESC, id DESC);
            ALTER TABLE content_history_events
                ALTER COLUMN description TYPE text;
            CREATE TABLE IF NOT EXISTS content_subscriptions(
                id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                user_id text NOT NULL, content_id integer NOT NULL, email text NOT NULL,
                UNIQUE(content_id,user_id));
            CREATE TABLE IF NOT EXISTS content_notification_queue(
                event_id bigint NOT NULL REFERENCES content_history_events(id),
                user_id text NOT NULL, email text NOT NULL, sent boolean NOT NULL DEFAULT false,
                PRIMARY KEY(event_id,user_id));
            """;

        await using var command = _dataSource.CreateCommand(sql);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ContentHistoryEventDto>> GetByContentIdAsync(
        int contentId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, content_id, occurred_at, description, author
            FROM content_history_events
            WHERE content_id = $1
            ORDER BY occurred_at DESC, id DESC;
            """;

        var result = new List<ContentHistoryEventDto>();
        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue(contentId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new ContentHistoryEventDto(
                reader.GetInt64(0),
                reader.GetInt32(1),
                reader.GetFieldValue<DateTimeOffset>(2),
                reader.GetString(3),
                reader.GetString(4)));
        }

        return result;
    }

    public async Task<ContentHistoryEventDto> AddAsync(
        int contentId,
        string description,
        string author,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            WITH change AS (
                INSERT INTO content_history_events (content_id, description, author)
                VALUES ($1, $2, $3)
                RETURNING id, occurred_at, content_id
            ), queued AS (
                INSERT INTO content_notification_queue(event_id, user_id, email)
                SELECT change.id, subscription.user_id, subscription.email
                FROM change JOIN content_subscriptions subscription
                    ON subscription.content_id = change.content_id
            )
            SELECT id, occurred_at FROM change;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue(contentId);
        command.Parameters.AddWithValue(description);
        command.Parameters.AddWithValue(author);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);

        var change = new ContentHistoryEventDto(
            reader.GetInt64(0),
            contentId,
            reader.GetFieldValue<DateTimeOffset>(1),
            description,
            author);
        await reader.DisposeAsync();
        await _publisher.PublishAsync(change, cancellationToken);
        return change;
    }

    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();
}
