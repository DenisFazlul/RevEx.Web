using System.ComponentModel.DataAnnotations;

namespace RevEx.Application.DTOs.ContentVersion;

public sealed class UpdateContentVersionDto
{
    /// <summary>Пользовательское название версии.</summary>
    [Required, MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    /// <summary>Дата выпуска или создания версии.</summary>
    public DateTimeOffset Date { get; init; }

    /// <summary>Состояние версии в простом жизненном цикле.</summary>
    [Required, RegularExpression("^(draft|published|archived)$")]
    public string Status { get; init; } = string.Empty;

    /// <summary>Приложение, для которого подготовлена версия. В MVP поддерживается Revit.</summary>
    [Required, RegularExpression("^revit$")]
    public string Application { get; init; } = "revit";

    /// <summary>Год версии Revit, в которой сохранено семейство.</summary>
    [Required, RegularExpression("^20[0-9]{2}$")]
    public string ApplicationVersion { get; init; } = string.Empty;
}
