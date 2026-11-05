using System.ComponentModel.DataAnnotations;

namespace RevEx.Api.Models;

public sealed class UploadFileRequest
{
    /// <summary>Загружаемый файл контента.</summary>
    [Required]
    public IFormFile File { get; init; } = null!;

    /// <summary>Назначение файла в версии: primary, type-catalog, lookup-table или attachment.</summary>
    [Required, MaxLength(32)]
    public string Role { get; init; } = "attachment";

    /// <summary>Необязательный JSON-объект с метаданными любого формата файла.</summary>
    [MaxLength(8 * 1024 * 1024)]
    public string? MetadataJson { get; init; }
}
