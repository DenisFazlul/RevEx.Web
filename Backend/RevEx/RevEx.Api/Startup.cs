using Microsoft.EntityFrameworkCore;
using RevEx.Infrastructure.Persistence;
using RevEx.Infrastructure.Notifications;
using RevEx.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApiControllers()
    .AddApiSwagger(builder.Configuration)
    .AddConfiguredAuthentication(builder.Configuration)
    .AddFrontendCors()
    .AddDatabase(builder.Configuration)
    .AddInfrastructureServices(builder.Configuration);

builder.Services.AddNotificationService(builder.Configuration);
var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();
    await db.Database.MigrateAsync();
}

await app.SeedInitialDataAsync();

app.UseApiSwagger();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<RevEx.Api.Middleware.DesktopCompatibilityMiddleware>();
app.MapControllers();

app.Run();
