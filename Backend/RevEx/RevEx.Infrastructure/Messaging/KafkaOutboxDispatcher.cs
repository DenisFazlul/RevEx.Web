using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace RevEx.Infrastructure.Messaging;

public sealed class KafkaOutboxDispatcher(KafkaOutboxStore store, KafkaProducer producer,
    ILogger<KafkaOutboxDispatcher> logger) : BackgroundService
{
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await store.InitializeAsync(cancellationToken);
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await store.DispatchOneAsync(producer.SendAsync, stoppingToken)) continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception) { logger.LogError(exception, "Kafka outbox dispatch failed"); }
            try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
        }
    }
}
