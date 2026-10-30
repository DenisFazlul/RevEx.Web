using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace RevEx.Infrastructure.Notifications;

public sealed record EmailNotification(IReadOnlyCollection<string> To, string Subject, string Body,
    IReadOnlyCollection<string>? Cc = null, bool IsHtml = false);

public interface INotificationService
{
    Task SendAsync(EmailNotification notification, CancellationToken cancellationToken = default);
}

public sealed class SmtpOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromAddress { get; set; } = "noreply@example.com";
    public string FromName { get; set; } = "RevEx";
}

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

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNotificationService(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SmtpOptions>().Bind(configuration.GetSection("NotificationService"))
            .Validate(o => !string.IsNullOrWhiteSpace(o.Host), "SMTP host is required.")
            .Validate(o => o.Port is > 0 and <= 65535, "Invalid SMTP port.")
            .Validate(o => MailAddress.TryCreate(o.FromAddress, out _), "Invalid sender address.");
        services.AddTransient<INotificationService, SmtpNotificationService>();
        services.AddSingleton<IContentSubscriptions, ContentSubscriptions>();
        services.AddSingleton<IContentChangeHandler, ContentChangeHandler>();
        services.AddOptions<ContentChangeConsumerOptions>().Bind(configuration.GetSection("Kafka"))
            .Validate(o => !string.IsNullOrWhiteSpace(o.BootstrapServers)
                && !string.IsNullOrWhiteSpace(o.Topic)
                && !string.IsNullOrWhiteSpace(o.NotificationGroupId),
                "Kafka notification consumer settings are required.")
            .ValidateOnStart();
        services.AddHostedService<KafkaContentNotificationConsumer>();
        return services;
    }
}
