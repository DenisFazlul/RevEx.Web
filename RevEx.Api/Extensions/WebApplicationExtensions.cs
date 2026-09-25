using RevEx.Application.Abstractions.Persistence;
using RevEx.Application.Abstractions.Storage;
using RevEx.Domain.Entities;
using RevEx.Infrastructure.Data;
using ContentEntity = RevEx.Domain.Entities.Content;

namespace RevEx.Api.Extensions;

public static class WebApplicationExtensions
{
    public static async Task SeedInitialDataAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        await InitialDataSeeder.SeedAsync(
            scope.ServiceProvider.GetRequiredService<IRepository<Category>>(),
            scope.ServiceProvider.GetRequiredService<IRepository<ContentStatus>>(),
            scope.ServiceProvider.GetRequiredService<IRepository<TagGroup>>(),
            scope.ServiceProvider.GetRequiredService<IRepository<ContentEntity>>(),
            scope.ServiceProvider.GetRequiredService<IRepository<ContentVersion>>(),
            scope.ServiceProvider.GetRequiredService<IRepository<ContentFile>>(),
            scope.ServiceProvider.GetRequiredService<IFileStorage>(),
            scope.ServiceProvider.GetRequiredService<IUnitOfWork>());
    }

    public static WebApplication UseApiSwagger(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.RoutePrefix = "swagger";
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "RevEx API v1");
        });

        return app;
    }
}
