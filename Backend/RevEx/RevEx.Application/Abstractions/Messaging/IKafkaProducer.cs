namespace RevEx.Application.Abstractions.Messaging;

public interface IKafkaProducer
{
    Task PublishAsync<T>(string topic, string key, T message, CancellationToken cancellationToken = default);
}
