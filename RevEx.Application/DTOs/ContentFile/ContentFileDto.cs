namespace RevEx.Application.DTOs.ContentFile;

public sealed record ContentFileDto(
    int Id,
    string Url,
    int? ContentVersionId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
