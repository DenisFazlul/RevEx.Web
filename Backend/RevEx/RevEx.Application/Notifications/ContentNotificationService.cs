using RevEx.Domain.Contracts.ContentHistory;
namespace RevEx.Application.Notifications;
public sealed class ContentNotificationService(INotificationDeliveryStore deliveries, INotificationService notifications)
{
    public async Task HandleAsync(ContentHistoryEventDto change, CancellationToken cancellationToken)
    {
        var recipients = await deliveries.GetPendingAsync(change, cancellationToken);
        foreach (var recipient in recipients)
        {
            await notifications.SendAsync(new EmailNotification([recipient.Email],
                $"RevEx: изменение контента {change.ContentId}",
                $"{change.Description}\nАвтор: {change.Author}\nВремя (UTC): {change.OccurredAt:u}"), cancellationToken);
            await deliveries.MarkDeliveredAsync(change.Id, recipient.UserId, cancellationToken);
        }
    }
}
