namespace RevEx.Application.Notifications;

public interface INotificationService
{
    Task SendAsync(EmailNotification notification, CancellationToken cancellationToken = default);
}
