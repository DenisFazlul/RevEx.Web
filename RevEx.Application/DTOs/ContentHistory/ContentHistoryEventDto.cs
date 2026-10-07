namespace RevEx.Application.DTOs.ContentHistory;

public sealed record ContentHistoryEventDto(
    long Id,
    int ContentId,
    DateTimeOffset OccurredAt,
    string Description,
    string Author);

public sealed record CreateContentHistoryEventDto(string Description);
