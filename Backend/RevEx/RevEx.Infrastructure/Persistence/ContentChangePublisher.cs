using System.Globalization;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RevEx.Domain.Contracts.ContentHistory;

namespace RevEx.Infrastructure.Persistence;

public sealed class ContentChangePublisher : IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly string _topic;
    private readonly ILogger<ContentChangePublisher> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public ContentChangePublisher(IOptions<KafkaOptions> options, ILogger<ContentChangePublisher> logger)
    {
        _topic = options.Value.Topic;
        _logger = logger;
        _producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = options.Value.BootstrapServers,
            EnableIdempotence = true,
            Acks = Acks.All,
            MessageTimeoutMs = 10000
        }).Build();
    }

    public async Task PublishAsync(ContentHistoryEventDto change, CancellationToken token)
    {
        try
        {
            await _producer.ProduceAsync(_topic, new Message<string, string>
            {
                Key = change.ContentId.ToString(CultureInfo.InvariantCulture),
                Value = JsonSerializer.Serialize(change, JsonOptions)
            }, token);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Kafka delivery failed for history event {EventId}; history is saved, delivery will not be retried.", change.Id);
        }
    }

    public void Dispose() => _producer.Dispose();
}
