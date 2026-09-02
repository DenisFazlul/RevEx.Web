namespace RevEx.Application.DTOs.Content;

public sealed record ContentDto(
    int Id,
    string Name,
    string Description,
    int CategoryId,
    int ContentStatusId,
    int? PreviewFileId,
    IReadOnlyCollection<int> TagIds,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
