using RevEx.Application.Subscriptions;
using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace RevEx.Infrastructure.Notifications;

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
