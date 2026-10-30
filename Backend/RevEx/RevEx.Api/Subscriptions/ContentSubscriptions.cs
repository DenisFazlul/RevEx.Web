using RevEx.Domain.Contracts.ContentHistory;
using Microsoft.Extensions.Options;
using Npgsql;
using NotificationService;
using RevEx.Infrastructure.Persistence;

namespace RevEx.Api.Subscriptions;

public interface IContentSubscriptions
{
    Task<bool> GetAsync(int contentId, string userId, CancellationToken token);
    Task SetAsync(int contentId, string userId, string? email, bool enabled, CancellationToken token);
}

public sealed class ContentSubscriptions(IOptions<ContentHistoryOptions> options,
    INotificationService notifications)
    : IContentSubscriptions, IContentChangeHandler
{
    private readonly string _connectionString = options.Value.ConnectionString;

    public async Task<bool> GetAsync(int contentId, string userId, CancellationToken token)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(token);
        await using var command = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM content_subscriptions WHERE content_id=$1 AND user_id=$2)", connection);
        command.Parameters.AddWithValue(contentId);
        command.Parameters.AddWithValue(userId);
        return (bool)(await command.ExecuteScalarAsync(token))!;
    }

    public async Task SetAsync(int contentId, string userId, string? email, bool enabled, CancellationToken token)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(token);
        await using var command = new NpgsqlCommand(enabled
            ? "INSERT INTO content_subscriptions(content_id,user_id,email) VALUES($1,$2,$3) ON CONFLICT(content_id,user_id) DO UPDATE SET email=EXCLUDED.email"
            : "DELETE FROM content_subscriptions WHERE content_id=$1 AND user_id=$2", connection);
        command.Parameters.AddWithValue(contentId);
        command.Parameters.AddWithValue(userId);
        if (enabled) command.Parameters.AddWithValue(email!);
        await command.ExecuteNonQueryAsync(token);
    }

    public async Task HandleAsync(ContentHistoryEventDto change, CancellationToken cancellationToken)
    {
        var recipients = new List<(string UserId, string Email)>();
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using (var command = new NpgsqlCommand("""
            SELECT s.user_id,s.email FROM content_subscriptions s
            WHERE s.content_id=$1 AND s.subscribed_at <= $2
                AND NOT EXISTS(SELECT 1 FROM content_notification_deliveries d
                    WHERE d.event_id=$3 AND d.user_id=s.user_id)
            """, connection))
        {
            command.Parameters.AddWithValue(change.ContentId);
            command.Parameters.AddWithValue(change.OccurredAt);
            command.Parameters.AddWithValue(change.Id);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                recipients.Add((reader.GetString(0), reader.GetString(1)));
        }

        foreach (var recipient in recipients)
        {
            // Failures propagate to the consumer: the Kafka offset must remain uncommitted.
            await notifications.SendAsync(new EmailNotification([recipient.Email],
                $"RevEx: изменение контента {change.ContentId}",
                $"{change.Description}\nАвтор: {change.Author}\nВремя (UTC): {change.OccurredAt:u}"),
                cancellationToken);
            await using var delivered = new NpgsqlCommand("""
                INSERT INTO content_notification_deliveries(event_id,user_id)
                VALUES($1,$2) ON CONFLICT DO NOTHING
                """, connection);
            delivered.Parameters.AddWithValue(change.Id);
            delivered.Parameters.AddWithValue(recipient.UserId);
            await delivered.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}