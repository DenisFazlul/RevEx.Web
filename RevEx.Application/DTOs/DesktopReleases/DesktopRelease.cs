namespace RevEx.Application.DTOs.DesktopReleases;

public sealed record DesktopReleaseManifest(
    string Version, string Notes, Dictionary<string, string> Installers);
public sealed record DesktopReleaseRecord(DesktopReleaseManifest Manifest, DateTimeOffset UploadedAt, DateTimeOffset? DeletedAt = null,
    DateTimeOffset? PublishedAt = null, bool IsActive = false,
    Dictionary<string, DesktopPlatformState>? Platforms = null);
public sealed record DesktopPlatformState(DateTimeOffset? PublishedAt = null, bool IsActive = false);
public sealed record DesktopPlatformTarget(string Version, string Installer, string Notes);
public sealed record DesktopReleaseEdit(string Notes);
public sealed record DesktopReleaseActivation(string Version, bool IsActive = true, string? Platform = null);
public sealed record DesktopReleaseInfo(string BackendVersion, string RecommendedDesktopVersion,
    string UpdatesPath,
    Dictionary<string, string> Installers, string Notes, Dictionary<string, DesktopPlatformTarget>? Targets = null);

public static class DesktopVersions
{
    public static Version Parse(string value)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(value ?? "", @"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$", System.Text.RegularExpressions.RegexOptions.CultureInvariant)
            || !Version.TryParse(value, out var version))
            throw new ArgumentException("Версия должна иметь формат 1.2.3 без префикса v.");
        return version;
    }
}

public sealed record DesktopPackage(string Version, string Platform, string FileName, long Size, string Sha256, string Sha1);
public sealed record DesktopPolicy(string Decision, string TargetVersion, string Notes, DesktopPackage? Package, bool IsDowngrade);
