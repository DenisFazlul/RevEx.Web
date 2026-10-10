using RevEx.Application.DTOs.DesktopReleases;

namespace RevEx.Application.Abstractions.Storage;

public interface IDesktopReleaseStore
{
    Task<IReadOnlyList<DesktopReleaseRecord>> ListAsync(CancellationToken cancellationToken);
    Task<DesktopReleaseRecord> UploadAsync(Stream archive, CancellationToken cancellationToken);
    Task ActivateAsync(DesktopReleaseActivation activation, CancellationToken cancellationToken);
    Task<DesktopReleaseRecord> EditAsync(string version, DesktopReleaseEdit edit, CancellationToken cancellationToken);
    Task DeleteAsync(string version, CancellationToken cancellationToken);
    Task RestoreAsync(string version, CancellationToken cancellationToken);
    Task<DesktopReleaseInfo?> GetActiveAsync(CancellationToken cancellationToken);
    Task<DesktopPolicy?> CheckAsync(string version, string platform, CancellationToken cancellationToken);
    Task<bool> IsSupportedAsync(string version, CancellationToken cancellationToken);
    Task<string?> GetPublishedPackageAsync(string version, string fileName, CancellationToken cancellationToken);
    Task<string?> GetPublicFileAsync(string version, string fileName, CancellationToken cancellationToken);
}
