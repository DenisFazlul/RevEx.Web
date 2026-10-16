namespace RevEx.Application.Abstractions.Storage;

public sealed record RevitRelease(string Id, string Version, string RevitVersion, string Notes,
    string FileName, DateTimeOffset UploadedAt, bool IsPublished);

public interface IRevitReleaseStore
{
    Task<IReadOnlyList<RevitRelease>> ListAsync(CancellationToken ct);
    Task<RevitRelease> UploadAsync(string version, string revitVersion, string notes, string fileName, Stream file, CancellationToken ct);
    Task<bool> PublishAsync(string id, bool published, CancellationToken ct);
    Task<(string Path, string Name)?> GetFileAsync(string id, CancellationToken ct);
}
