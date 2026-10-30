namespace RevEx.Infrastructure.Messaging;

public sealed record KafkaOutboxMessage(Guid Id, string Topic, string Key, string Payload);
