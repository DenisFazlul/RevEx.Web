using System.ComponentModel.DataAnnotations;

namespace RevEx.Api.Models;

public sealed class UploadFileRequest
{
    [Required]
    public IFormFile File { get; init; } = null!;
}
