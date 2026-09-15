using System.ComponentModel.DataAnnotations;

namespace RevEx.Application.DTOs.Content;

public sealed class UpdateContentDto
{
    /// <summary>Новое название контента.</summary>
    [Required, MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    /// <summary>Новое описание контента.</summary>
    [Required, MaxLength(2000)]
    public string Description { get; init; } = string.Empty;

    /// <summary>Идентификатор новой категории каталога.</summary>
    [Range(1, int.MaxValue)]
    public int CategoryId { get; init; }

    /// <summary>Необязательный идентификатор нового состояния карточки.</summary>
    [Range(1, int.MaxValue)]
    public int? ContentStatusId { get; init; }

    /// <summary>Новый набор идентификаторов тегов; null сохраняет текущие теги.</summary>
    public IReadOnlyCollection<int>? TagIds { get; init; }
}
