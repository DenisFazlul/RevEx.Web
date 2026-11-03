using RevEx.Infrastructure.Repositories;
using RevEx.Application.Abstractions.Messaging;
using RevEx.Infrastructure.Messaging;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Infrastructure.Persistence;

namespace RevEx.Api.Extensions;

public static class DatabaseServiceCollectionExtensions
{
    public static IServiceCollection AddDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<MongoDbOptions>(configuration.GetRequiredSection(MongoDbOptions.SectionName));
        services.AddScoped<MongoDbContext>();
        services.AddScoped<RevEx.Application.Comments.ICommentStore, PostgresCommentStore>();
        services.Configure<ContentHistoryOptions>(
            configuration.GetRequiredSection(ContentHistoryOptions.SectionName));
        services.AddSingleton<PostgresContextFactory>();
        services.AddScoped<PostgresDbContext>(sp => sp.GetRequiredService<PostgresContextFactory>().Create());
        services.AddSingleton<IContentHistoryStore, PostgresContentHistoryStore>();
        services.AddSingleton<IDesktopUpdateTrackingStore, PostgresDesktopUpdateTrackingStore>();

        services.AddOptions<KafkaOptions>().Bind(configuration.GetSection("Kafka"))
            .Validate(o => !string.IsNullOrWhiteSpace(o.BootstrapServers) && !string.IsNullOrWhiteSpace(o.Topic), "Kafka settings are required.")
            .ValidateOnStart();
        services.AddSingleton<KafkaOutboxStore>();
        services.AddSingleton<KafkaProducer>();
        services.AddSingleton<IKafkaProducer>(sp => sp.GetRequiredService<KafkaProducer>());
        services.AddHostedService<KafkaOutboxDispatcher>();
        services.AddSingleton<ContentChangePublisher>();
        return services;
    }
}
