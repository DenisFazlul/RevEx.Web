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
builder.Services.AddSingleton<RevEx.Api.Subscriptions.ContentSubscriptions>();
builder.Services.AddSingleton<RevEx.Api.Subscriptions.IContentSubscriptions>(sp => sp.GetRequiredService<RevEx.Api.Subscriptions.ContentSubscriptions>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<RevEx.Api.Subscriptions.ContentSubscriptions>());
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
