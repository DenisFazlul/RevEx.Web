using RevEx.Application.Abstractions.Persistence;
using RevEx.Application.Abstractions.Storage;
using RevEx.Infrastructure.Persistence;
using RevEx.Infrastructure.Repositories;
using RevEx.Infrastructure.Storage;
using RevEx.Infrastructure.DesktopReleases;

namespace RevEx.Api.Extensions;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton(configuration.GetSection("DesktopReleases").Get<DesktopReleaseOptions>() ?? new DesktopReleaseOptions());
        services.AddSingleton<IDesktopReleaseStore, FileDesktopReleaseStore>();
        services.AddSingleton<IRevitReleaseStore, FileRevitReleaseStore>();
        services.AddScoped<IContentRepository, PostgresContentRepository>();
        services.AddScoped<IContentFileRepository, PostgresContentFileRepository>();
        services.AddScoped<IContentFileMetadataRepository, PostgresContentFileMetadataRepository>();
        services.AddScoped<ITagGroupRepository, PostgresTagGroupRepository>();
        services.AddScoped<IContentStatusRepository, PostgresContentStatusRepository>();
        services.AddScoped<ICategoryRepository, PostgresCategoryRepository>();
        services.AddScoped<ITagRepository, PostgresTagRepository>();
        services.AddScoped<IContentVersionRepository, PostgresContentVersionRepository>();
        services.AddScoped<IContentQueries, PostgresContentQueries>();

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
