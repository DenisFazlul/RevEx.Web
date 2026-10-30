using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RevEx.Domain.Contracts.ContentHistory;

namespace RevEx.Infrastructure.Notifications;

/// <summary>Consumes the history publisher's JSON messages and acknowledges only delivered events.</summary>
public sealed class KafkaContentNotificationConsumer(
    IOptions<ContentChangeConsumerOptions> options,
    IContentChangeHandler handler,
    ILogger<KafkaContentNotificationConsumer> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.Run(() => ConsumeAsync(stoppingToken), stoppingToken);

    private async Task ConsumeAsync(CancellationToken token)
    {
        var settings = options.Value;
        using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = settings.BootstrapServers,
            GroupId = settings.NotificationGroupId,
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            MaxPollIntervalMs = 900000
        }).Build();
        consumer.Subscribe(settings.Topic);
        try
        {
            while (!token.IsCancellationRequested)
            {
                ConsumeResult<string, string> message;
                try { message = consumer.Consume(token); }
                catch (ConsumeException exception)
                {
                    logger.LogError(exception, "Cannot consume content change from Kafka");
                    await Task.Delay(TimeSpan.FromSeconds(5), token);
                    continue;
                }

                // Keep this message pending until handling and explicit offset commit succeed.
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await ProcessAsync(message.Message.Value, token);
                        consumer.Commit(message);
                        break;
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                    catch (Exception exception)
                    {
                        logger.LogError(exception,
                            "Content notification failed at {Position}; offset is not committed, retrying",
                            message.TopicPartitionOffset);
                        await Task.Delay(TimeSpan.FromSeconds(5), token);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        finally { consumer.Close(); }
    }

    public async Task ProcessAsync(string json, CancellationToken token)
    {
        var change = JsonSerializer.Deserialize<ContentHistoryEventDto>(json, JsonOptions)
            ?? throw new JsonException("Content history event is null.");
        if (change.Id <= 0 || change.ContentId <= 0 || string.IsNullOrWhiteSpace(change.Description)
            || string.IsNullOrWhiteSpace(change.Author) || change.OccurredAt == default)
            throw new JsonException("Content history event is missing required fields.");
        await handler.HandleAsync(change, token);
    }
}
