using RevEx.Application.Notifications;
using RevEx.Domain.Contracts.ContentHistory;
namespace RevEx.Infrastructure.Notifications;
public sealed class ContentChangeHandler(ContentNotificationService service) : IContentChangeHandler
{
    public Task HandleAsync(ContentHistoryEventDto change, CancellationToken token) => service.HandleAsync(change, token);
}
