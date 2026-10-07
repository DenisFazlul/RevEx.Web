using System.ComponentModel.DataAnnotations;

namespace RevEx.Application.DTOs.Tag;

public sealed record TagDto(
    /* Идентификатор тега. */ int Id,
    /* Название тега. */ string Name,
    /* Идентификатор группы. */ int TagGroupId,
    /* Название группы. */ string TagGroupName,
    /* Дата создания тега. */ DateTimeOffset CreatedAt,
    /* Дата последнего изменения тега. */ DateTimeOffset UpdatedAt);

public sealed class CreateTagDto
{
    /// <summary>Название создаваемого тега.</summary>
    [Required, MaxLength(100)]
    public string Name { get; init; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int TagGroupId { get; init; }
}

public sealed class UpdateTagDto
{
    /// <summary>Новое название тега.</summary>
    [Required, MaxLength(100)]
    public string Name { get; init; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int TagGroupId { get; init; }
}
