using System.ComponentModel.DataAnnotations;

namespace RevEx.Application.DTOs.Tag;

public sealed record TagDto(
    int Id,
    string Name,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed class CreateTagDto
{
    [Required, MaxLength(100)]
    public string Name { get; init; } = string.Empty;
}

public sealed class UpdateTagDto
{
    [Required, MaxLength(100)]
    public string Name { get; init; } = string.Empty;
}
