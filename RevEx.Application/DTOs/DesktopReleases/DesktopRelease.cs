namespace RevEx.Application.DTOs.DesktopReleases;

public sealed record DesktopReleaseManifest(
    string Version, string Notes, Dictionary<string, string> Installers);
public sealed record DesktopReleaseRecord(DesktopReleaseManifest Manifest, DateTimeOffset UploadedAt);
public sealed record DesktopReleaseActivation(string Version, string MinimumDesktopVersion, string MaximumDesktopVersion);
public sealed record DesktopReleaseInfo(string BackendVersion, string RecommendedDesktopVersion,
    string MinimumDesktopVersion, string MaximumDesktopVersion, string UpdatesPath,
    Dictionary<string, string> Installers, string Notes);

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
