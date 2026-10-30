using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RevEx.Domain.Contracts.ContentHistory;

namespace RevEx.Infrastructure.Notifications;

public interface IContentChangeHandler
{
    Task HandleAsync(ContentHistoryEventDto change, CancellationToken cancellationToken);
}
