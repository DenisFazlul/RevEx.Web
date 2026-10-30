
namespace RevEx.Application.Abstractions.Storage;

public sealed record RevitRelease(string Id, string Version, string RevitVersion, string Notes,
    string FileName, DateTimeOffset UploadedAt, bool IsPublished);
