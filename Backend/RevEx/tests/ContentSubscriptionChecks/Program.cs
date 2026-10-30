using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Infrastructure.Persistence;

var settings = Environment.GetEnvironmentVariable("REVEX_TEST_POSTGRES")
    ?? throw new InvalidOperationException("Set REVEX_TEST_POSTGRES to a test PostgreSQL connection string.");
var schema = "subscription_checks_" + Guid.NewGuid().ToString("N");
await using var admin = NpgsqlDataSource.Create(settings);
async Task Execute(string sql)
{
    await using var command = admin.CreateCommand(sql);
    await command.ExecuteNonQueryAsync();
}
await Execute($"CREATE SCHEMA {schema}");
try
{
    var connection = new NpgsqlConnectionStringBuilder(settings) { SearchPath = schema }.ConnectionString;
    using var publisher = new ContentChangePublisher(Options.Create(new KafkaOptions
    {
        BootstrapServers = "localhost:1",
        Topic = "subscription-checks"
    }), NullLogger<ContentChangePublisher>.Instance);
    await using var implementation = new PostgresContentHistoryStore(
        Options.Create(new ContentHistoryOptions { ConnectionString = connection }), publisher);
    IContentHistoryStore store = implementation;
    await store.InitializeAsync();
    await using var data = NpgsqlDataSource.Create(connection);
    async Task Run(string sql)
    {
        await using var command = data.CreateCommand(sql);
        await command.ExecuteNonQueryAsync();
    }
    async Task<long> Count(string sql)
    {
        await using var command = data.CreateCommand(sql);
        return (long)(await command.ExecuteScalarAsync())!;
    }
    void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        Console.WriteLine("PASS: " + message);
    }
    // Simulate the trigger installed by the previous version, then initialize again.
    await Run("""
        CREATE FUNCTION queue_content_notifications() RETURNS trigger AS $$
        BEGIN RETURN NEW; END; $$ LANGUAGE plpgsql;
        CREATE TRIGGER content_history_notifications AFTER INSERT ON content_history_events
        FOR EACH ROW EXECUTE FUNCTION queue_content_notifications();
        """);
    await store.InitializeAsync();
    Check(await Count("SELECT count(*) FROM pg_trigger WHERE tgname='content_history_notifications' AND tgrelid='content_history_events'::regclass") == 0,
        "Initialization removes the old trigger.");

    await Run("""
        INSERT INTO content_subscriptions(content_id,user_id,email)
        VALUES(991,'first','first@example.com'),(991,'second','second@example.com'),(992,'other','other@example.com');
        """);
    var change = await store.AddAsync(991, "Updated description", "tester");
    Check(await Count($"SELECT count(*) FROM content_notification_queue WHERE event_id={change.Id}") == 2,
        "IContentHistoryStore.AddAsync queues only this content's subscribers.");
    Check((await store.GetByContentIdAsync(991)).Single().Description == change.Description,
        "The same event is available in history.");
    await Run("DELETE FROM content_subscriptions WHERE user_id='second'");
    var next = await store.AddAsync(991, "Next change", "tester");
    Check(await Count($"SELECT count(*) FROM content_notification_queue WHERE event_id={next.Id}") == 1,
        "Unsubscribed user gets no new notification.");

    await Run("INSERT INTO content_history_events(content_id,description,author) VALUES(991,'Direct SQL','tester')");
    Check(await Count("SELECT count(*) FROM content_notification_queue") == 3,
        "Direct SQL inserts do not send notifications.");

    var before = await Count("SELECT count(*) FROM content_history_events");
    await Run("ALTER TABLE content_notification_queue ADD CONSTRAINT simulate_failure CHECK (event_id < 0) NOT VALID");
    try
    {
        await store.AddAsync(991, "Must roll back", "tester");
        throw new Exception("Queue failure was expected.");
    }
    catch (PostgresException exception) when (exception.SqlState == "23514") { }
    Check(await Count("SELECT count(*) FROM content_history_events") == before,
        "Queue failure also rolls back the history event.");
}
finally
{
    await Execute($"DROP SCHEMA {schema} CASCADE");
}
