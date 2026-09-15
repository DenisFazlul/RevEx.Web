using System.ComponentModel.DataAnnotations;

namespace RevEx.Application.DTOs.Tag;

public sealed record TagDto(
    /* Идентификатор тега. */ int Id,
    /* Название тега. */ string Name,
    /* Дата создания тега. */ DateTimeOffset CreatedAt,
    /* Дата последнего изменения тега. */ DateTimeOffset UpdatedAt);

public sealed class CreateTagDto
{
    /// <summary>Название создаваемого тега.</summary>
    [Required, MaxLength(100)]
    public string Name { get; init; } = string.Empty;
}

public sealed class UpdateTagDto
{
    /// <summary>Новое название тега.</summary>
    [Required, MaxLength(100)]
    public string Name { get; init; } = string.Empty;
}
