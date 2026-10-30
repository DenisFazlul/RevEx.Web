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
        services.AddSingleton<MongoDbContext>();
        services.Configure<ContentHistoryOptions>(
            configuration.GetRequiredSection(ContentHistoryOptions.SectionName));
        services.AddSingleton<IContentHistoryStore, PostgresContentHistoryStore>();
        services.AddSingleton<IDesktopUpdateTrackingStore, PostgresDesktopUpdateTrackingStore>();

        services.AddOptions<KafkaOptions>().Bind(configuration.GetSection("Kafka"))
            .Validate(o => !string.IsNullOrWhiteSpace(o.BootstrapServers) && !string.IsNullOrWhiteSpace(o.Topic), "Kafka settings are required.")
            .ValidateOnStart();
        services.AddHostedService<RevEx.Api.ContentChangePublisher>();
        return services;
    }
}
