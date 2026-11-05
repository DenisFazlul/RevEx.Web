namespace RevEx.Application.Catalog;

public sealed record VersionFileUpload(
    string FileName,
    long Length,
    Func<Stream> OpenReadStream,
    string Role,
    string? MetadataJson = null);
