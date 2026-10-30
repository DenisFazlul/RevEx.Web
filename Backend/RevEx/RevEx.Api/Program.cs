using NotificationService;
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

await app.InitializeContentHistoryAsync();
await app.SeedInitialDataAsync();

app.UseApiSwagger();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<RevEx.Api.Middleware.DesktopCompatibilityMiddleware>();
app.MapControllers();

app.Run();

public partial class Program;
