using RevEx.Domain.Contracts.ContentHistory;
namespace RevEx.Application.Notifications;
public interface INotificationDeliveryStore
{
    Task<IReadOnlyCollection<NotificationRecipient>> GetPendingAsync(ContentHistoryEventDto change, CancellationToken token);
    Task MarkDeliveredAsync(long eventId, string userId, CancellationToken token);
}
