namespace RevEx.Infrastructure.Persistence;
public sealed class OutboxEntry
{
public Guid Id {get;set;} public required string Topic {get;set;} public required string MessageKey {get;set;} public required string Payload {get;set;} public DateTimeOffset CreatedAt {get;set;} public int Attempts {get;set;} public DateTimeOffset NextAttemptAt {get;set;} public string? LastError {get;set;}
}
