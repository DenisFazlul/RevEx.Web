using Microsoft.Extensions.Options;
using Npgsql;
using RevEx.Domain.Contracts.ContentHistory;
using RevEx.Infrastructure.Persistence;

namespace RevEx.Infrastructure.Notifications;

public sealed class ContentChangeHandler(
    IOptions<ContentHistoryOptions> options,
    INotificationService notifications) : IContentChangeHandler
{
    private readonly string _connectionString = options.Value.ConnectionString;

    public async Task HandleAsync(ContentHistoryEventDto change, CancellationToken cancellationToken)
    {
        await SendNotificationsAsync(change, cancellationToken);
    }

    private async Task SendNotificationsAsync(ContentHistoryEventDto change, CancellationToken cancellationToken)
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