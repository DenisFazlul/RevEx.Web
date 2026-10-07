namespace RevEx.Infrastructure.DesktopReleases;

public sealed class DesktopReleaseOptions
{
    public string Path { get; set; } = "storage/desktop-releases";
    public long MaximumExpandedBytes { get; set; } = 2L * 1024 * 1024 * 1024;
}
