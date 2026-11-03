using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RevEx.Infrastructure.Persistence;
namespace RevEx.Infrastructure.Messaging;
public sealed class KafkaOutboxStore(IOptions<ContentHistoryOptions> options) : IAsyncDisposable
{
    private readonly PostgresContextFactory factory = new(options);
    public async Task AddAsync(KafkaOutboxMessage message, CancellationToken token)
    {
        await using var db = factory.Create();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO kafka_outbox(id,topic,message_key,payload) VALUES({message.Id},{message.Topic},{message.Key},{message.Payload})", token);
    }
    public async Task<bool> DispatchOneAsync(Func<KafkaOutboxMessage, CancellationToken, Task> send, CancellationToken token)
    {
        await using var db = factory.Create();
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var rows = await db.Database.SqlQueryRaw<PendingOutboxMessage>("""
            SELECT id,topic,message_key,payload FROM kafka_outbox
            WHERE next_attempt_at <= now() ORDER BY created_at,id LIMIT 1 FOR UPDATE SKIP LOCKED
            """).ToListAsync(token);
        if (rows.Count == 0) return false;
        var row = rows[0];
        string? error = null;
        try { await send(new(row.Id, row.Topic, row.Key, row.Payload), token); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception exception) { error = exception.Message; }
        if (error == null) await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM kafka_outbox WHERE id={row.Id}", token);
        else await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE kafka_outbox SET attempts=attempts+1,last_error={error},next_attempt_at=now()+interval '30 seconds' WHERE id={row.Id}", token);
        await transaction.CommitAsync(token);
        return true;
    }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
