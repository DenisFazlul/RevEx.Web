using RevEx.Application.Subscriptions;
using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace RevEx.Infrastructure.Notifications;

public interface INotificationService
{
    Task SendAsync(EmailNotification notification, CancellationToken cancellationToken = default);
}
