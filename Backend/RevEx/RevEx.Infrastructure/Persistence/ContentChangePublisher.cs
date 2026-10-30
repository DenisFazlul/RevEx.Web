using System.Globalization;
using Microsoft.Extensions.Options;
using RevEx.Application.Abstractions.Messaging;
using RevEx.Domain.Contracts.ContentHistory;

namespace RevEx.Infrastructure.Persistence;

public sealed class ContentChangePublisher(IOptions<KafkaOptions> options, IKafkaProducer producer)
{
    public Task PublishAsync(ContentHistoryEventDto change, CancellationToken token) =>
        producer.PublishAsync(options.Value.Topic,
            change.ContentId.ToString(CultureInfo.InvariantCulture), change, token);
}
