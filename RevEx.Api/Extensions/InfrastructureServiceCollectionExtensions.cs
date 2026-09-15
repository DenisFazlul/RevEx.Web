using RevEx.Application.Abstractions.Persistence;
using RevEx.Application.Abstractions.Storage;
using RevEx.Infrastructure.Persistence;
using RevEx.Infrastructure.Repositories;
using RevEx.Infrastructure.Storage;

namespace RevEx.Api.Extensions;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddSingleton<IFileStorage, InMemoryFileStorage>();

        return services;
    }
}
