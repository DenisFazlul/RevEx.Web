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
        services.AddScoped<RevEx.Application.Comments.ICommentStore, PostgresCommentStore>();
        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetRequiredSection(DatabaseOptions.SectionName))
            .Validate(
                options => IsValidConnectionString(options.ConnectionString),
                "Database:ConnectionString must contain Host, Database and Username.")
            .ValidateOnStart();
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

    private static bool IsValidConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return false;
        }

        try
        {
            var settings = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);
            return !string.IsNullOrWhiteSpace(settings.Host) &&
                !string.IsNullOrWhiteSpace(settings.Database) &&
                !string.IsNullOrWhiteSpace(settings.Username);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
