using System.ComponentModel.DataAnnotations;

namespace RevEx.Application.DTOs.ContentStatus;

public sealed record ContentStatusDto(
    int Id,
    string Name,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed class CreateContentStatusDto
{
    [Required, MaxLength(100)]
    public string Name { get; init; } = string.Empty;
}

public sealed class UpdateContentStatusDto
{
    [Required, MaxLength(100)]
    public string Name { get; init; } = string.Empty;
}
