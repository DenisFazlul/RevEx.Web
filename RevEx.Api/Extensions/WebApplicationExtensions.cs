using RevEx.Application.Abstractions.Storage;
using RevEx.Infrastructure.Data;

namespace RevEx.Api.Extensions;

public static class WebApplicationExtensions
{
    public static async Task SeedInitialDataAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        await InitialDataSeeder.SeedAsync(
            scope.ServiceProvider.GetRequiredService<AppDbContext>(),
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
