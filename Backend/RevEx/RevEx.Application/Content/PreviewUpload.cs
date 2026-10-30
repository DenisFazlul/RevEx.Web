namespace RevEx.Application.Content;

public sealed record PreviewUpload(string FileName, string ContentType, long Length, Func<Stream> OpenReadStream)
{
    public const long MaximumFileSizeBytes = 10 * 1024 * 1024;
    public static IReadOnlyDictionary<string, string> MediaTypeByExtension { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".png"] = "image/png",
            [".webp"] = "image/webp", [".gif"] = "image/gif"
        };
    public static bool IsSupported(PreviewUpload file) =>
        file.Length is > 0 and <= MaximumFileSizeBytes &&
        MediaTypeByExtension.TryGetValue(Path.GetExtension(file.FileName), out var mediaType) &&
        string.Equals(file.ContentType, mediaType, StringComparison.OrdinalIgnoreCase);
}
