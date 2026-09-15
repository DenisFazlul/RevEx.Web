using System.ComponentModel.DataAnnotations;

namespace RevEx.Application.DTOs.Content;

public sealed class CreateContentDto
{
    /// <summary>Название создаваемого контента.</summary>
    [Required, MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    /// <summary>Описание назначения и состава контента.</summary>
    [Required, MaxLength(2000)]
    public string Description { get; init; } = string.Empty;

    /// <summary>Идентификатор категории каталога.</summary>
    [Range(1, int.MaxValue)]
    public int CategoryId { get; init; }

    /// <summary>Необязательный идентификатор состояния; по умолчанию используется draft.</summary>
    [Range(1, int.MaxValue)]
    public int? ContentStatusId { get; init; }

    /// <summary>Необязательный набор идентификаторов тегов.</summary>
    public IReadOnlyCollection<int>? TagIds { get; init; }
}
