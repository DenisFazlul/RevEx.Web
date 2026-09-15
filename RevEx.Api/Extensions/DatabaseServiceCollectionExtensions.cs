using Microsoft.EntityFrameworkCore;
using RevEx.Infrastructure.Data;

namespace RevEx.Api.Extensions;

public static class DatabaseServiceCollectionExtensions
{
    public static IServiceCollection AddDatabase(this IServiceCollection services)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase("RevEx"));

        return services;
    }
}
