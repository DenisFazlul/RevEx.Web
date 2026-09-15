using System.ComponentModel.DataAnnotations;

namespace RevEx.Application.DTOs.Category;

public sealed class CreateCategoryDto
{
    /// <summary>Название создаваемой категории.</summary>
    [Required, MaxLength(200)]
    public string Name { get; init; } = string.Empty;
}
