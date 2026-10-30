using RevEx.Application.Subscriptions;
using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace RevEx.Infrastructure.Notifications;

public sealed class SmtpNotificationService(IOptions<SmtpOptions> options) : INotificationService
{
    public async Task SendAsync(EmailNotification notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);
        if (notification.To is null || notification.To.Count == 0)
            throw new ArgumentException("At least one recipient is required.", nameof(notification));
        cancellationToken.ThrowIfCancellationRequested();
        var settings = options.Value;
        using var message = new MailMessage
        {
            From = new MailAddress(settings.FromAddress, settings.FromName),
            Subject = notification.Subject, Body = notification.Body, IsBodyHtml = notification.IsHtml
        };
        foreach (var address in notification.To) message.To.Add(new MailAddress(address));
        foreach (var address in notification.Cc ?? []) message.CC.Add(new MailAddress(address));
        using var client = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = settings.EnableSsl, UseDefaultCredentials = false,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };
        if (!string.IsNullOrWhiteSpace(settings.Username))
            client.Credentials = new NetworkCredential(settings.Username, settings.Password);
        await client.SendMailAsync(message, cancellationToken);
    }
}
