using RevEx.Api.Notifications;
using System.Text.Json;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NotificationService;
using Npgsql;
using RevEx.Api.Subscriptions;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Domain.Contracts.ContentHistory;
using RevEx.Infrastructure.Persistence;

var settings = Environment.GetEnvironmentVariable("REVEX_TEST_POSTGRES")
    ?? throw new InvalidOperationException("Set REVEX_TEST_POSTGRES.");
var kafka = Environment.GetEnvironmentVariable("REVEX_TEST_KAFKA")
    ?? throw new InvalidOperationException("Set REVEX_TEST_KAFKA.");
var suffix = Guid.NewGuid().ToString("N");
var schema = "subscription_checks_" + suffix;
var topic = "subscription-checks-" + suffix;
await using var adminDb = NpgsqlDataSource.Create(settings);
using var adminKafka = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = kafka }).Build();
await using (var command = adminDb.CreateCommand($"CREATE SCHEMA {schema}"))
    await command.ExecuteNonQueryAsync();
try
{
    await adminKafka.CreateTopicsAsync([new TopicSpecification { Name = topic, NumPartitions = 1, ReplicationFactor = 1 }]);
    var connection = new NpgsqlConnectionStringBuilder(settings) { SearchPath = schema }.ConnectionString;
    using var publisher = new ContentChangePublisher(Options.Create(new KafkaOptions
    {
        BootstrapServers = kafka, Topic = topic
    }), NullLogger<ContentChangePublisher>.Instance);
    await using var implementation = new PostgresContentHistoryStore(
        Options.Create(new ContentHistoryOptions { ConnectionString = connection }), publisher);
    IContentHistoryStore history = implementation;
    await history.InitializeAsync();
    var sender = new RecordingNotifications();
    var subscriptions = new ContentSubscriptions(
        Options.Create(new ContentHistoryOptions { ConnectionString = connection }));
    var handler = new ContentChangeHandler(
        Options.Create(new ContentHistoryOptions { ConnectionString = connection }), sender);
    var consumerOptions = Options.Create(new ContentChangeConsumerOptions
    {
        BootstrapServers = kafka, Topic = topic, NotificationGroupId = "checks-" + suffix
    });
    using var consumer = new KafkaContentNotificationConsumer(
        consumerOptions, handler, NullLogger<KafkaContentNotificationConsumer>.Instance);
    await subscriptions.SetAsync(991, "first", "first@example.com", true, default);
    await subscriptions.SetAsync(991, "second", "second@example.com", true, default);
    await subscriptions.SetAsync(992, "other", "other@example.com", true, default);
    async Task WaitForDeliveries(long expected)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var db = NpgsqlDataSource.Create(connection);
        while (true)
        {
            await using var query = db.CreateCommand("SELECT count(*) FROM content_notification_deliveries");
            if ((long)(await query.ExecuteScalarAsync(deadline.Token))! == expected) return;
            await Task.Delay(50, deadline.Token);
        }
    }
    var change = await history.AddAsync(991, "Updated description", "tester");
    Check(sender.Sent.Count == 0, "AddAsync publishes to Kafka without sending email.");
    // Start after publication to verify delivery from Kafka, including retained events.
    await consumer.StartAsync(default);
    try
    {
        await WaitUntil(() => sender.Sent.Count == 2);
        Check(sender.Sent.SelectMany(x => x.To).Order().SequenceEqual(
            new[] { "first@example.com", "second@example.com" }),
            "Kafka event reaches only the content's subscribers.");
        Check(sender.Sent.All(x => x.Body.Contains(change.Description) && x.Body.Contains(change.Author)),
            "Messages contain the published history description and author.");

        await WaitForDeliveries(2);
        await consumer.ProcessAsync(JsonSerializer.Serialize(change, new JsonSerializerOptions(JsonSerializerDefaults.Web)), default);
        Check(sender.Sent.Count == 2, "Replayed event does not resend successful deliveries.");

        await subscriptions.SetAsync(991, "second", null, false, default);
        var next = await history.AddAsync(991, "Next change", "tester");
        await WaitUntil(() => sender.Sent.Count == 3);
        Check(sender.Sent.Last().To.Single() == "first@example.com", "Unsubscribed user receives no new notification.");

        await subscriptions.SetAsync(991, "late", "late@example.com", true, default);
        await WaitForDeliveries(3);
        await consumer.ProcessAsync(JsonSerializer.Serialize(next, new JsonSerializerOptions(JsonSerializerDefaults.Web)), default);
        Check(sender.Sent.Count == 3, "New subscriber receives no earlier event on replay.");

        sender.FailNext = true;
        await history.AddAsync(991, "Retry failed delivery", "tester");
        await WaitUntil(() => sender.Failures > 0);
        await WaitUntil(() => sender.Sent.Count == 5);
        Check(sender.Sent.Count == 5, "SMTP failure is retried from the pending Kafka message.");
        using var offsetReader = new ConsumerBuilder<string,string>(new ConsumerConfig
        {
            BootstrapServers = kafka, GroupId = consumerOptions.Value.NotificationGroupId
        }).Build();
        await WaitUntil(() => offsetReader.Committed([new TopicPartition(topic, 0)], TimeSpan.FromSeconds(5))
            .Single().Offset.Value == 3);
        Check(true, "Consumer commits Kafka offsets after successful delivery.");

        var count = sender.Sent.Count;
        try { await consumer.ProcessAsync("{}", default); throw new Exception("Invalid event accepted"); }
        catch (JsonException) { }
        Check(sender.Sent.Count == count, "Malformed event sends no email.");
        Check((await history.GetByContentIdAsync(991)).Count == 3, "All changes remain in history.");
    }
    finally { await consumer.StopAsync(default); }
}
finally
{
    await adminKafka.DeleteTopicsAsync([topic]);
    await using var command = adminDb.CreateCommand($"DROP SCHEMA {schema} CASCADE");
    await command.ExecuteNonQueryAsync();
}

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    Console.WriteLine("PASS: " + message);
}
static async Task WaitUntil(Func<bool> condition)
{
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
    while (!condition()) await Task.Delay(100, deadline.Token);
}
sealed class RecordingNotifications : INotificationService
{
    public System.Collections.Concurrent.ConcurrentQueue<EmailNotification> Sent { get; } = new();
    public volatile bool FailNext;
    public int Failures;
    public Task SendAsync(EmailNotification notification, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (FailNext)
        {
            FailNext = false;
            Interlocked.Increment(ref Failures);
            throw new InvalidOperationException("Simulated SMTP failure");
        }
        Sent.Enqueue(notification);
        return Task.CompletedTask;
    }
}
