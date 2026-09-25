using RevEx.Application.Abstractions.Persistence;
using RevEx.Application.Abstractions.Storage;
using RevEx.Infrastructure.Persistence;
using RevEx.Infrastructure.Repositories;
using RevEx.Infrastructure.Storage;

namespace RevEx.Api.Extensions;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped(typeof(IRepository<>), typeof(MongoRepository<>));
        services.AddScoped<IUnitOfWork, MongoUnitOfWork>();

        var options = configuration
            .GetRequiredSection(FileStorageOptions.SectionName)
            .Get<FileStorageOptions>()
            ?? throw new InvalidOperationException("FileStorage configuration is missing.");

        services.AddSingleton<IFileStorage>(_ => options.Provider.Trim().ToLowerInvariant() switch
        {
            "memory" => new InMemoryFileStorage(),
            "s3" => new S3FileStorage(options.S3),
            "networkfolder" => new NetworkFolderFileStorage(options.NetworkFolder),
            _ => throw new InvalidOperationException(
                $"Unsupported FileStorage provider '{options.Provider}'. " +
                "Supported values: Memory, S3, NetworkFolder.")
        });

        return services;
    }
}
