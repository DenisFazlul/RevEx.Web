using System.ComponentModel.DataAnnotations;

namespace RevEx.Application.DTOs.ContentStatus;

public sealed record ContentStatusDto(
    /* Идентификатор состояния. */ int Id,
    /* Системное название состояния. */ string Name,
    /* Дата создания состояния. */ DateTimeOffset CreatedAt,
    /* Дата последнего изменения состояния. */ DateTimeOffset UpdatedAt);

public sealed class CreateContentStatusDto
{
    /// <summary>Название создаваемого состояния контента.</summary>
    [Required, MaxLength(100)]
    public string Name { get; init; } = string.Empty;
}

public sealed class UpdateContentStatusDto
{
    /// <summary>Новое название состояния контента.</summary>
    [Required, MaxLength(100)]
    public string Name { get; init; } = string.Empty;
}
