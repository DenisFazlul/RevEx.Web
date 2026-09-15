using System.ComponentModel.DataAnnotations;

namespace RevEx.Application.DTOs.ContentVersion;

public sealed class CreateContentVersionDto
{
    /// <summary>Идентификатор контента, для которого создаётся версия.</summary>
    [Range(1, int.MaxValue)]
    public int ContentId { get; init; }

    /// <summary>Пользовательское название версии.</summary>
    [MaxLength(200)]
    public string? Name { get; init; }

    /// <summary>Дата выпуска или создания версии.</summary>
    public DateTimeOffset? Date { get; init; }

    /// <summary>Состояние версии в простом жизненном цикле.</summary>
    [RegularExpression("^(draft|published|archived)$")]
    public string? Status { get; init; }

    /// <summary>Приложение, для которого подготовлена версия. В MVP поддерживается Revit.</summary>
    [Required, RegularExpression("^revit$")]
    public string Application { get; init; } = "revit";

    /// <summary>Год версии Revit, в которой сохранено семейство.</summary>
    [Required, RegularExpression("^20[0-9]{2}$")]
    public string ApplicationVersion { get; init; } = string.Empty;
}
