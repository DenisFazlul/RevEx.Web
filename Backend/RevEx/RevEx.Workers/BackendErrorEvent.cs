namespace RevEx.Workers;

public sealed record BackendErrorEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    string TraceId,
    string Method,
    string? Route,
    string ExceptionType,
    string Environment);
