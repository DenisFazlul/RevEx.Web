using Microsoft.Extensions.Options;
using Npgsql;
using RevEx.Infrastructure.Persistence;

namespace RevEx.Infrastructure.Messaging;

public sealed class KafkaOutboxStore(IOptions<ContentHistoryOptions> options) : IAsyncDisposable
{
    private readonly NpgsqlDataSource source = NpgsqlDataSource.Create(options.Value.ConnectionString);

    public async Task InitializeAsync(CancellationToken token)
    {
        await using var command = source.CreateCommand("""
            CREATE TABLE IF NOT EXISTS kafka_outbox (
                id uuid PRIMARY KEY, topic text NOT NULL, message_key text NOT NULL,
                payload text NOT NULL, created_at timestamptz NOT NULL DEFAULT now(),
                attempts integer NOT NULL DEFAULT 0, next_attempt_at timestamptz NOT NULL DEFAULT now(),
                last_error text);
            CREATE INDEX IF NOT EXISTS ix_kafka_outbox_pending ON kafka_outbox(next_attempt_at);
            """);
        await command.ExecuteNonQueryAsync(token);
    }

    public async Task AddAsync(KafkaOutboxMessage message, CancellationToken token)
    {
        await using var command = source.CreateCommand("""
            INSERT INTO kafka_outbox(id,topic,message_key,payload) VALUES($1,$2,$3,$4)
            """);
        command.Parameters.AddWithValue(message.Id);
        command.Parameters.AddWithValue(message.Topic);
        command.Parameters.AddWithValue(message.Key);
        command.Parameters.AddWithValue(message.Payload);
        await command.ExecuteNonQueryAsync(token);
    }

    public async Task<bool> DispatchOneAsync(Func<KafkaOutboxMessage, CancellationToken, Task> send, CancellationToken token)
    {
        await using var connection = await source.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        await using var select = new NpgsqlCommand("""
            SELECT id,topic,message_key,payload FROM kafka_outbox
            WHERE next_attempt_at <= now() ORDER BY created_at,id
            LIMIT 1 FOR UPDATE SKIP LOCKED
            """, connection, transaction);
        KafkaOutboxMessage message;
        await using (var reader = await select.ExecuteReaderAsync(token))
        {
            if (!await reader.ReadAsync(token)) return false;
            message = new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3));
        }
        string? error = null;
        try { await send(message, token); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception exception) { error = exception.Message; }
        await using var update = new NpgsqlCommand(error is null
            ? "DELETE FROM kafka_outbox WHERE id=$1"
            : "UPDATE kafka_outbox SET attempts=attempts+1,last_error=$2,next_attempt_at=now()+interval '30 seconds' WHERE id=$1",
            connection, transaction);
        update.Parameters.AddWithValue(message.Id);
        if (error is not null) update.Parameters.AddWithValue(error);
        await update.ExecuteNonQueryAsync(token);
        await transaction.CommitAsync(token);
        return true;
    }

    public ValueTask DisposeAsync() => source.DisposeAsync();
}
