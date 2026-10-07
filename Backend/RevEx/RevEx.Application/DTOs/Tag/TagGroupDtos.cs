using System.ComponentModel.DataAnnotations;

namespace RevEx.Application.DTOs.Tag;

public sealed record TagGroupDto(
    int Id,
    string Name,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed class CreateTagGroupDto
{
    [Required, MaxLength(100)]
    public string Name { get; init; } = string.Empty;
}

public sealed class UpdateTagGroupDto
{
    [Required, MaxLength(100)]
    public string Name { get; init; } = string.Empty;
}
