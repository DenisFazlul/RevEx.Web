namespace RevEx.Infrastructure.Persistence;
public sealed class KafkaOptions
{
    public string BootstrapServers { get; set; } = "localhost:9092";
    public string Topic { get; set; } = "content-changes";
}
