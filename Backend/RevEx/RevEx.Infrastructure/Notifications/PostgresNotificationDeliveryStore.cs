using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RevEx.Application.Notifications;
using RevEx.Domain.Contracts.ContentHistory;
using RevEx.Infrastructure.Persistence;

namespace RevEx.Infrastructure.Notifications;

public sealed class PostgresNotificationDeliveryStore(IOptions<DatabaseOptions> options) : INotificationDeliveryStore
{
    private readonly PostgresContextFactory factory = new(options);

    public async Task<IReadOnlyCollection<NotificationRecipient>> GetPendingAsync(
        ContentHistoryEventDto change,
        CancellationToken token)
    {
        await using var database = factory.Create();
        var recipients = await database.Database.SqlQuery<NotificationRecipient>($"""
            SELECT s.user_id AS "UserId", s.email AS "Email"
            FROM content_subscriptions s
            WHERE s.content_id = {change.ContentId}
                AND s.subscribed_at <= {change.OccurredAt}
                AND NOT EXISTS(
                    SELECT 1
                    FROM content_notification_deliveries d
                    WHERE d.event_id = {change.Id} AND d.user_id = s.user_id)
            """).ToListAsync(token);
        return recipients;
    }

    public async Task MarkDeliveredAsync(long eventId, string userId, CancellationToken token)
    {
        await using var database = factory.Create();
        await database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO content_notification_deliveries(event_id, user_id)
            VALUES({eventId}, {userId})
            ON CONFLICT DO NOTHING
            """, token);
    }
}
