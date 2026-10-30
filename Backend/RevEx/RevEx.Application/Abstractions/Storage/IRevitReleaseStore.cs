
namespace RevEx.Application.Abstractions.Storage;

public interface IRevitReleaseStore
{
    Task<IReadOnlyList<RevitRelease>> ListAsync(CancellationToken ct);
    Task<RevitRelease> UploadAsync(string version, string revitVersion, string notes, string fileName, Stream file, CancellationToken ct);
    Task<bool> PublishAsync(string id, bool published, CancellationToken ct);
    Task<(string Path, string Name)?> GetFileAsync(string id, CancellationToken ct);
}
