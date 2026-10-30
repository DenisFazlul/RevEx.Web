using RevEx.Application.Content;
namespace RevEx.Api.Models;

public static class ContentPreviewUploadConfiguration
{
    public const long MaximumFileSizeBytes = PreviewUpload.MaximumFileSizeBytes;
    public static IReadOnlyDictionary<string, string> MediaTypeByExtension => PreviewUpload.MediaTypeByExtension;
    public static bool IsSupported(IFormFile file) => PreviewUpload.IsSupported(
        new PreviewUpload(file.FileName, file.ContentType, file.Length, file.OpenReadStream));
}
