using System.ComponentModel.DataAnnotations;

namespace RevEx.Application.DTOs.DesktopReleases;

public sealed record DesktopInstallationRequest(
    Guid InstallationId,
    [Required, RegularExpression("^[A-Fa-f0-9]{64}$")] string InstallationKey,
    [Required, StringLength(32)] string Version,
    [Required, RegularExpression("^(win-x64|osx-x64|osx-arm64)$")] string Platform,
    Guid? CompletedOfferId = null);
public sealed record DesktopUpdateOffer(Guid Id, Guid InstallationId, string FromVersion,
    DesktopPolicy Policy, DateTimeOffset OfferedAt, DateTimeOffset? ConfirmedAt = null,
    string? UserId = null, string? UserName = null, string? Failure = null);
public sealed record DesktopUpdateResponse(string Decision, string TargetVersion, string Notes,
    DesktopPackage? Package, bool IsDowngrade, Guid? OfferId, string? DownloadPath);
public sealed record DesktopInstallationInfo(Guid InstallationId, string Platform, string Version,
    DateTimeOffset LastSeenAt, string? UserId, string? UserName);
public sealed record DesktopDownloadInfo(Guid Id, Guid? OfferId, Guid? InstallationId, string Version,
    string FileName, DateTimeOffset StartedAt, DateTimeOffset? FinishedAt, string Status,
    string? UserId, string? UserName);
public sealed record DesktopUpdateHistory(IReadOnlyList<DesktopInstallationInfo> Installations,
    IReadOnlyList<DesktopUpdateOffer> Updates, IReadOnlyList<DesktopDownloadInfo> Downloads);
public sealed record DesktopUpdateFailure([Required, StringLength(2000)] string Message);
