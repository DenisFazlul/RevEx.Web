using System.ComponentModel.DataAnnotations.Schema;
namespace RevEx.Infrastructure.Messaging;
public sealed class PendingOutboxMessage
{
    [Column("id")] public Guid Id { get; set; }
    [Column("topic")] public required string Topic { get; set; }
    [Column("message_key")] public required string Key { get; set; }
    [Column("payload")] public required string Payload { get; set; }
}
