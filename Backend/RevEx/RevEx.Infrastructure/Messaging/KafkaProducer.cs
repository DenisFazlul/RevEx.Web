using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using RevEx.Application.Abstractions.Messaging;
using RevEx.Infrastructure.Persistence;

namespace RevEx.Infrastructure.Messaging;

public sealed class KafkaProducer : IKafkaProducer, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IProducer<string, string> producer;
    private readonly KafkaOutboxStore outbox;

    public KafkaProducer(IOptions<KafkaOptions> options, KafkaOutboxStore outbox)
    {
        this.outbox = outbox;
        producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = options.Value.BootstrapServers,
            EnableIdempotence = true,
            Acks = Acks.All,
            MessageTimeoutMs = 10000
        }).Build();
    }

    public async Task PublishAsync<T>(string topic, string key, T message, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();

        var entry = new KafkaOutboxMessage(Guid.NewGuid(), topic, key,
            JsonSerializer.Serialize(message, JsonOptions));
        try
        {
            await SendAsync(entry, cancellationToken);
        }
        catch (ProduceException<string, string>)
        {
            // The request may already be cancelled; persist the fallback independently.
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await outbox.AddAsync(entry, timeout.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await outbox.AddAsync(entry, timeout.Token);
            throw;
        }
    }

    internal async Task SendAsync(KafkaOutboxMessage entry, CancellationToken token)
    {
        await producer.ProduceAsync(entry.Topic, new Message<string, string>
        {
            Key = entry.Key,
            Value = entry.Payload
        }, token);
    }
    public void Dispose() => producer.Dispose();
}

