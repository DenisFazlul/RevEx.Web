namespace RevEx.Application.DTOs.ContentVersion;

public sealed record ContentVersionDto(
    int Id,
    int ContentId,
    IReadOnlyCollection<int> FileIds,
    string Name,
    DateTimeOffset Date,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
