using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RevEx.Domain.Contracts.ContentHistory;

namespace RevEx.Infrastructure.Notifications;

public sealed class ContentChangeConsumerOptions
{
    public string BootstrapServers { get; set; } = "localhost:9092";
    public string Topic { get; set; } = "content-changes";
    public string NotificationGroupId { get; set; } = "revex-content-notifications";
}
