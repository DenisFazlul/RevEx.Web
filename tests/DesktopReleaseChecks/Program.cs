using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RevEx.Application.DTOs.DesktopReleases;
using RevEx.Infrastructure.DesktopReleases;

var root = Path.Combine(Path.GetTempPath(), "revex-release-checks-" + Guid.NewGuid().ToString("N"));
var options = new DesktopReleaseOptions { Path = root };
var store = new FileDesktopReleaseStore(options);
var ct = CancellationToken.None;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception(name);
    Console.WriteLine("PASS: " + name);
}
async Task Reject(Func<Task> action, string name)
{
    try { await action(); }
    catch (Exception exception) when (exception is ArgumentException or InvalidOperationException) { Check(true, name); return; }
    throw new Exception("Expected rejection: " + name);
}

try
{
    Check(await store.GetActiveAsync(ct) == null, "No active release before activation");
    using var archive = Archive("2.4.1");
    await store.UploadAsync(archive, ct);
    Check((await store.ListAsync(ct)).Count == 1, "Valid release staged");
    Check(await store.GetPublicFileAsync("2.4.1", "RevEx-win-x64.exe", ct) == null, "Staged installer is private");
    using var duplicate = Archive("2.4.1");
    await Reject(() => store.UploadAsync(duplicate, ct), "Published version cannot be overwritten");
    await Reject(() => store.ActivateAsync(new("2.4.1", "2.5.0", "2.5.1"), ct), "Recommended version must be in range");
    await Reject(() => store.ActivateAsync(new("2.4.2", "2.4.0", "2.4.2"), ct), "Missing release cannot be activated");
    await store.ActivateAsync(new("2.4.1", "2.4.0", "2.4.1"), ct);
    var active = await store.GetActiveAsync(ct);
    Check(active?.RecommendedDesktopVersion == "2.4.1" && active.MinimumDesktopVersion == "2.4.0", "Atomic activation and compatibility policy");
    Check(await store.GetPublicFileAsync("2.4.1", "RevEx-win-x64.exe", ct) != null, "Active installer can be downloaded");
    Check(await store.GetPublicFileAsync("2.4.1", "../active.json", ct) == null, "Traversal rejected");
    Check(await store.GetPublicFileAsync("2.4.1", "record.json", ct) == null, "Internal metadata is private");
    Check((await new FileDesktopReleaseStore(options).GetActiveAsync(ct))?.RecommendedDesktopVersion == "2.4.1", "Activation survives store restart");
    using var traversal = Archive("2.4.2", extra: "../escape.txt");
    await Reject(() => store.UploadAsync(traversal, ct), "ZIP traversal rejected");
    Check(!File.Exists(Path.Combine(root, "escape.txt")), "ZIP traversal wrote no external file");
    using var checksum = Archive("2.4.2", badHash: true);
    await Reject(() => store.UploadAsync(checksum, ct), "Corrupted checksum rejected");
    using var wrongPlatform = Archive("2.4.2", wrongPlatform: true);
    await Reject(() => store.UploadAsync(wrongPlatform, ct), "Package channel mismatch rejected");
    using var extra = Archive("2.4.2", extra: "surprise.exe");
    await Reject(() => store.UploadAsync(extra, ct), "Unexpected files rejected");
    Check((await store.ListAsync(ct)).Count == 1 && !Directory.EnumerateDirectories(root).Any(p => Path.GetFileName(p).StartsWith('.')), "Rejected uploads leave no staged release");
    var limited = new FileDesktopReleaseStore(new() { Path = Path.Combine(root, "limited"), MaximumExpandedBytes = 32 });
    using var large = Archive("2.4.2");
    await Reject(() => limited.UploadAsync(large, ct), "Expanded size limit enforced");
    var package = Path.Combine(root, "2.4.1", "RevEx-2.4.1-win-x64-full.nupkg");
    await File.AppendAllTextAsync(package, "tampered");
    await Reject(() => store.ActivateAsync(new("2.4.1", "2.4.1", "2.4.1"), ct), "Tampering after upload prevents reactivation");
    if (args.Length == 2 && args[0] == "--write-fixture")
    {
        using var fixture = Archive("99.0.1");
        await File.WriteAllBytesAsync(args[1], fixture.ToArray());
        Console.WriteLine("Test-only fixture created (not a runnable desktop installer).");
    }
}
finally { Directory.Delete(root, recursive: true); }

static MemoryStream Archive(string version, string? extra = null, bool badHash = false, bool wrongPlatform = false)
{
    var stream = new MemoryStream();
    using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
    {
        var installers = new Dictionary<string, string>();
        foreach (var platform in new[] { "win-x64", "osx-x64", "osx-arm64" })
        {
            var installer = $"RevEx-{platform}." + (platform == "win-x64" ? "exe" : "pkg");
            installers.Add(platform, installer);
            Add(zip, installer, Encoding.UTF8.GetBytes("TEST FIXTURE ONLY"));
            using var packageStream = new MemoryStream();
            using (var package = new ZipArchive(packageStream, ZipArchiveMode.Create, true))
                Add(package, "RevEx.Desktop.nuspec", Encoding.UTF8.GetBytes($"<package><metadata><id>RevEx.Desktop</id><version>{version}</version><channel>{(wrongPlatform ? "wrong" : platform)}</channel><rid>{platform}</rid></metadata></package>"));
            var bytes = packageStream.ToArray();
            var name = $"RevEx-{version}-{platform}-full.nupkg";
            Add(zip, name, bytes);
            Add(zip, $"releases.{platform}.json", JsonSerializer.SerializeToUtf8Bytes(new { Assets = new[] { new {
                PackageId = "RevEx.Desktop", Version = version, Type = "Full", FileName = name, Size = bytes.Length,
                SHA256 = badHash ? "bad" : Convert.ToHexString(SHA256.HashData(bytes)), SHA1 = Convert.ToHexString(SHA1.HashData(bytes))
            } } }));
        }
        Add(zip, "release.json", JsonSerializer.SerializeToUtf8Bytes(new { version, notes = "Test fixture", installers }));
        if (extra != null) Add(zip, extra, [1, 2, 3]);
    }
    stream.Position = 0;
    return stream;
}
static void Add(ZipArchive zip, string name, byte[] bytes)
{
    using var output = zip.CreateEntry(name).Open();
    output.Write(bytes);
}
