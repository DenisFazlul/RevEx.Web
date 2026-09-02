using System.ComponentModel.DataAnnotations;

namespace RevEx.Application.DTOs.Content;

public sealed class CreateContentDto
{
    [Required, MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    [Required, MaxLength(2000)]
    public string Description { get; init; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int CategoryId { get; init; }

    [Range(1, int.MaxValue)]
    public int? ContentStatusId { get; init; }

    public IReadOnlyCollection<int>? TagIds { get; init; }
}
