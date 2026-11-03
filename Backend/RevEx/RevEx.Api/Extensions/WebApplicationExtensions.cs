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
            scope.ServiceProvider.GetRequiredService<ICategoryRepository>(),
            scope.ServiceProvider.GetRequiredService<IContentStatusRepository>(),
            scope.ServiceProvider.GetRequiredService<ITagGroupRepository>(),
            scope.ServiceProvider.GetRequiredService<IContentRepository>(),
            scope.ServiceProvider.GetRequiredService<IContentVersionRepository>(),
            scope.ServiceProvider.GetRequiredService<IContentFileRepository>(),
            scope.ServiceProvider.GetRequiredService<IFileStorage>());
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
