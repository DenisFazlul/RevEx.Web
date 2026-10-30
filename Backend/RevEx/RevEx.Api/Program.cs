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
builder.Services.AddSingleton<RevEx.Api.Subscriptions.IContentSubscriptions, RevEx.Api.Subscriptions.ContentSubscriptions>();
builder.Services.AddSingleton<IContentChangeHandler, RevEx.Api.Notifications.ContentChangeHandler>();
builder.Services.AddOptions<ContentChangeConsumerOptions>().Bind(builder.Configuration.GetSection("Kafka"))
    .Validate(o => !string.IsNullOrWhiteSpace(o.BootstrapServers) && !string.IsNullOrWhiteSpace(o.Topic)
        && !string.IsNullOrWhiteSpace(o.NotificationGroupId), "Kafka notification consumer settings are required.")
    .ValidateOnStart();
builder.Services.AddHostedService<KafkaContentNotificationConsumer>();
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
