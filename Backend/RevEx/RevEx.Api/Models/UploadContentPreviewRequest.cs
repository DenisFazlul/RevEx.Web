using System.ComponentModel.DataAnnotations;

namespace RevEx.Api.Models;

public sealed class UploadContentPreviewRequest
{
    /// <summary>Изображение-превью контента.</summary>
    [Required]
    public IFormFile File { get; init; } = null!;
}
