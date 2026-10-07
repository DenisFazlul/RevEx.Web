using RevEx.Application.DTOs.DesktopReleases;

namespace RevEx.Application.Abstractions.Storage;

public interface IDesktopReleaseStore
{
    Task<IReadOnlyList<DesktopReleaseRecord>> ListAsync(CancellationToken cancellationToken);
    Task<DesktopReleaseRecord> UploadAsync(Stream archive, CancellationToken cancellationToken);
    Task ActivateAsync(DesktopReleaseActivation activation, CancellationToken cancellationToken);
    Task<DesktopReleaseInfo?> GetActiveAsync(CancellationToken cancellationToken);
    Task<string?> GetPublicFileAsync(string version, string fileName, CancellationToken cancellationToken);
}
