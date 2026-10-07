namespace RevEx.Api.Models;

public static class ContentPreviewUploadConfiguration
{
    public const long MaximumFileSizeBytes = 10 * 1024 * 1024;

    public static IReadOnlyDictionary<string, string> MediaTypeByExtension { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
            [".webp"] = "image/webp",
            [".gif"] = "image/gif"
        };

    public static bool IsSupported(IFormFile file)
    {
        var extension = Path.GetExtension(file.FileName);
        return file.Length is > 0 and <= MaximumFileSizeBytes &&
               MediaTypeByExtension.TryGetValue(extension, out var expectedMediaType) &&
               string.Equals(file.ContentType, expectedMediaType, StringComparison.OrdinalIgnoreCase);
    }
}
