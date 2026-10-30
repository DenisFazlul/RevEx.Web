using RevEx.Infrastructure.Notifications;
using System.Text.Json;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Domain.Contracts.ContentHistory;
using RevEx.Infrastructure.Persistence;

sealed class RecordingNotifications : INotificationService
{
    public System.Collections.Concurrent.ConcurrentQueue<EmailNotification> Sent { get; } = new();
    public volatile bool FailNext;
    public int Failures;
    public Task SendAsync(EmailNotification notification, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (FailNext)
        {
            FailNext = false;
            Interlocked.Increment(ref Failures);
            throw new InvalidOperationException("Simulated SMTP failure");
        }
        Sent.Enqueue(notification);
        return Task.CompletedTask;
    }
}
