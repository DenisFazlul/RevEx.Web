using Microsoft.EntityFrameworkCore;
using RevEx.Infrastructure.Persistence;
using RevEx.Infrastructure.Notifications;
using RevEx.Api.Extensions;
using RevEx.Api.Middleware;
using RevEx.Workers;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApiControllers()
    .AddApiSwagger(builder.Configuration)
    .AddConfiguredAuthentication(builder.Configuration)
    .AddFrontendCors()
    .AddDatabase(builder.Configuration)
    .AddInfrastructureServices(builder.Configuration);

builder.Services.AddNotificationService(builder.Configuration);
builder.Services.AddSingleton<BackendErrorQueue>();
builder.Services.AddHostedService<BackendErrorWorker>();
var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();
    await db.Database.MigrateAsync();
}

await app.SeedInitialDataAsync();

app.UseApiSwagger();
app.UseCors("Frontend");
app.UseMiddleware<UnexpectedErrorMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<RevEx.Api.Middleware.DesktopCompatibilityMiddleware>();
app.MapControllers();

app.Run();
