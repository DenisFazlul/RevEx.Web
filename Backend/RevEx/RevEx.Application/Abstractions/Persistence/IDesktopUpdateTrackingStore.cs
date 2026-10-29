using RevEx.Domain.Contracts.DesktopReleases;

namespace RevEx.Application.Abstractions.Persistence;

public interface IDesktopUpdateTrackingStore
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task ObserveAsync(DesktopInstallationRequest request, string? userId, string? userName, CancellationToken cancellationToken);
    Task<DesktopUpdateOffer> CreateOfferAsync(DesktopInstallationRequest request, DesktopPolicy policy, CancellationToken cancellationToken);
    Task<DesktopUpdateOffer?> GetOfferAsync(Guid offerId, string installationKey, CancellationToken cancellationToken);
    Task FailOfferAsync(Guid offerId, string installationKey, string message, CancellationToken cancellationToken);
    Task<Guid> StartDownloadAsync(DesktopUpdateOffer? offer, string version, string fileName, string? userId, string? userName, CancellationToken cancellationToken);
    Task FinishDownloadAsync(Guid id, string status, CancellationToken cancellationToken);
    Task<DesktopUpdateHistory> GetHistoryAsync(int limit, CancellationToken cancellationToken);
}
