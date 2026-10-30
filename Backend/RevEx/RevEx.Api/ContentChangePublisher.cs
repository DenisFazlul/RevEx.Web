using System.Globalization;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using Npgsql;
using RevEx.Domain.Contracts.ContentHistory;
using RevEx.Infrastructure.Persistence;

namespace RevEx.Api;

public sealed class ContentChangePublisher(
    IOptions<ContentHistoryOptions> historyOptions,
    IOptions<KafkaOptions> kafkaOptions,
    ILogger<ContentChangePublisher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var source = NpgsqlDataSource.Create(historyOptions.Value.ConnectionString);
        using var producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = kafkaOptions.Value.BootstrapServers,
            EnableIdempotence = true,
            Acks = Acks.All,
            MessageTimeoutMs = 10000
        }).Build();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await PublishNextAsync(source, producer, stoppingToken)) continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Content change delivery failed; pending events will be retried.");
            }
            try { await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    private async Task<bool> PublishNextAsync(NpgsqlDataSource source,
        IProducer<string, string> producer, CancellationToken token)
    {
        await using var connection = await source.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        // One dispatcher at a time, including when multiple API replicas are running.
        await using var claim = new NpgsqlCommand("SELECT pg_try_advisory_xact_lock(72419021)", connection, transaction);
        if (await claim.ExecuteScalarAsync(token) is not true) return false;
        const string sql = """
            SELECT h.id, h.content_id, h.occurred_at, h.description, h.author
            FROM content_change_outbox o JOIN content_history_events h ON h.id = o.event_id
            ORDER BY o.event_id LIMIT 1 FOR UPDATE OF o;
            """;
        ContentHistoryEventDto change;
        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        await using (var reader = await command.ExecuteReaderAsync(token))
        {
            if (!await reader.ReadAsync(token)) return false;
            change = new ContentHistoryEventDto(reader.GetInt64(0), reader.GetInt32(1),
                reader.GetFieldValue<DateTimeOffset>(2), reader.GetString(3), reader.GetString(4));
        }
        await producer.ProduceAsync(kafkaOptions.Value.Topic, new Message<string, string>
        {
            Key = change.ContentId.ToString(CultureInfo.InvariantCulture),
            Value = JsonSerializer.Serialize(change, new JsonSerializerOptions(JsonSerializerDefaults.Web))
        }, token);
        await using var delete = new NpgsqlCommand(
            "DELETE FROM content_change_outbox WHERE event_id = $1", connection, transaction);
        delete.Parameters.AddWithValue(change.Id);
        await delete.ExecuteNonQueryAsync(token);
        await transaction.CommitAsync(token);
        return true;
    }
}
