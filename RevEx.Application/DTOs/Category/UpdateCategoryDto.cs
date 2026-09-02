using System.ComponentModel.DataAnnotations;

namespace RevEx.Application.DTOs.Category;

public sealed class UpdateCategoryDto
{
    [Required, MaxLength(200)]
    public string Name { get; init; } = string.Empty;
}
