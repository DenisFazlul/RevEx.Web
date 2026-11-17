using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace RevEx.Workers;

public sealed class BackendErrorWorker(
    BackendErrorQueue queue,
    IConfiguration configuration,
    ILogger<BackendErrorWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var bootstrapServers = configuration["Kafka:BootstrapServers"];
        if (string.IsNullOrWhiteSpace(bootstrapServers))
        {
            logger.LogWarning("Kafka is not configured. Backend error events will remain on disk.");
            return;
        }

        var topic = configuration["Kafka:ErrorTopic"] ?? "revex.backend.errors";
        using var producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            EnableIdempotence = true,
            MessageTimeoutMs = 10000
        }).Build();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                Directory.CreateDirectory(queue.DirectoryPath);
                foreach (var path in Directory.EnumerateFiles(queue.DirectoryPath, "*.json"))
                {
                    var payload = await File.ReadAllTextAsync(path, stoppingToken);
                    await producer.ProduceAsync(topic, new Message<string, string>
                    {
                        Key = Path.GetFileNameWithoutExtension(path),
                        Value = payload
                    }, stoppingToken);
                    File.Delete(path);
                }
            }
            catch (Exception exception) when (exception is KafkaException or IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(exception, "Could not drain the backend error queue. Retrying.");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
