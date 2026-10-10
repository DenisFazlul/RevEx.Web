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
    if (Environment.GetEnvironmentVariable("REVEX_TEST_POSTGRES") is { Length: > 0 } connection)
        await TrackingChecks.RunAsync(connection);
    Check(await store.GetActiveAsync(ct) == null, "No active release before activation");
    using var archive = Archive("2.4.1");
    await store.UploadAsync(archive, ct);
    Check((await store.ListAsync(ct)).Count == 1, "Valid release staged");
    Check(await store.GetPublicFileAsync("2.4.1", "RevEx-win-x64.exe", ct) == null, "Staged installer is private");
    using var duplicate = Archive("2.4.1");
    await Reject(() => store.UploadAsync(duplicate, ct), "Published version cannot be overwritten");
    await Reject(() => store.ActivateAsync(new("2.4.2"), ct), "Missing release cannot be activated");
    await store.ActivateAsync(new("2.4.1"), ct);
    var active = await store.GetActiveAsync(ct);
    Check(active?.RecommendedDesktopVersion == "2.4.1", "Published version becomes active target");
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
    await Reject(() => store.ActivateAsync(new("2.4.1"), ct), "Tampering after upload prevents reactivation");
    using var editable = Archive("2.4.2");
    await store.UploadAsync(editable, ct);
    var edited = await store.EditAsync("2.4.2", new("Updated description"), ct);
    Check(edited.Manifest.Version == "2.4.2" && edited.Manifest.Notes == "Updated description", "Description editable without changing package version");
    await Reject(() => store.EditAsync("2.4.2", new(new string('x', 12001)), ct), "Description length enforced");
    await Reject(() => store.DeleteAsync("2.4.1", ct), "Active release cannot be deleted");
    await store.DeleteAsync("2.4.2", ct);
    Check((await store.ListAsync(ct)).Single(r => r.Manifest.Version == "2.4.2").DeletedAt != null, "Inactive release moved to recoverable trash");
    Check(File.Exists(Path.Combine(root, ".trash", "2.4.2", "RevEx-win-x64.exe")), "Deleting preserves installer files");
    await Reject(() => store.ActivateAsync(new("2.4.2"), ct), "Deleted release cannot be activated");
    await Reject(() => store.EditAsync("2.4.2", new("not allowed"), ct), "Deleted release cannot be edited");
    using var reused = Archive("2.4.2");
    await Reject(() => store.UploadAsync(reused, ct), "Trashed version cannot be overwritten");
    await new FileDesktopReleaseStore(options).RestoreAsync("2.4.2", ct);
    Check((await store.ListAsync(ct)).Single(r => r.Manifest.Version == "2.4.2").DeletedAt == null, "Trash survives restart and restore clears deleted state");
    await store.ActivateAsync(new("2.4.2"), ct);
    await store.EditAsync("2.4.2", new("Active description"), ct);
    Check((await store.GetActiveAsync(ct))?.Notes == "Active description", "Editing active release updates public description");
    await store.ActivateAsync(new("2.4.1", false), ct);
    await Reject(() => store.DeleteAsync("2.4.1", ct), "Published packages cannot be deleted even when disabled");
    Check((await store.GetActiveAsync(ct))?.RecommendedDesktopVersion == "2.4.2", "Disabling old version leaves latest active target");
    await Reject(() => store.ActivateAsync(new("2.4.2", false), ct), "Last active version cannot be disabled");
    Check((await store.CheckAsync("2.4.2", "win-x64", ct))?.Decision == "current", "Current version accepted by backend");
    Check((await store.CheckAsync("2.4.1", "win-x64", ct))?.Decision == "required", "Disabled version requires update");
    Check((await store.CheckAsync("9.0.0", "win-x64", ct)) is { Decision: "required", IsDowngrade: true }, "Unknown newer client receives backend-selected downgrade");
    await Reject(() => store.CheckAsync("2.4.1", "linux-x64", ct), "Unsupported platform rejected");
    using var previousArchive = Archive("2.4.0");
    await store.UploadAsync(previousArchive, ct);
    await store.ActivateAsync(new("2.4.0"), ct);
    Check((await store.CheckAsync("2.4.0", "osx-arm64", ct)) is { Decision: "optional", Package.Platform: "osx-arm64" }, "Active older version gets optional platform-specific update");
    var pinned = (await store.CheckAsync("2.4.0", "win-x64", ct))!.Package!;
    await store.ActivateAsync(new("2.4.2", false), ct);
    Check((await store.GetActiveAsync(ct))?.RecommendedDesktopVersion == "2.4.0", "Disabling newest version selects previous active version");
    Check((await store.CheckAsync("2.4.2", "win-x64", ct)) is { Decision: "required", IsDowngrade: true, TargetVersion: "2.4.0" }, "Disabled newest client receives required rollback");
    Check(await store.GetPublishedPackageAsync(pinned.Version, pinned.FileName, ct) != null, "Previously issued package remains available after target changes");
    Check(await store.GetPublicFileAsync(pinned.Version, pinned.FileName, ct) != null, "Legacy clients can download the new updater via a published feed");
    Check((await new FileDesktopReleaseStore(options).GetActiveAsync(ct))?.RecommendedDesktopVersion == "2.4.0", "Multi-version activity survives restart");
    await Reject(() => store.DeleteAsync("../outside", ct), "Delete traversal rejected");
    await Reject(() => store.RestoreAsync("../outside", ct), "Restore traversal rejected");
    await Reject(() => store.DeleteAsync("2.4.9", ct), "Missing release delete rejected");
    using var competing = Archive("2.4.3");
    await store.UploadAsync(competing, ct);
    async Task<bool> Attempt(Func<Task> work)
    {
        try { await work(); return true; }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException) { return false; }
    }
    var race = await Task.WhenAll(
        Task.Run(() => Attempt(() => store.ActivateAsync(new("2.4.3"), ct))),
        Task.Run(() => Attempt(() => store.DeleteAsync("2.4.3", ct))));
    Check(race.Count(value => value) == 1 && await store.GetActiveAsync(ct) != null, "Concurrent activation and deletion cannot remove active files");
    var legacyOptions = new DesktopReleaseOptions { Path = Path.Combine(root, "legacy") };
    var legacyStore = new FileDesktopReleaseStore(legacyOptions);
    using var legacyArchive = Archive("1.0.0");
    await legacyStore.UploadAsync(legacyArchive, ct);
    await File.WriteAllTextAsync(Path.Combine(legacyOptions.Path, "active.json"), JsonSerializer.Serialize(new
        { version = "1.0.0", minimumDesktopVersion = "0.9.0", maximumDesktopVersion = "1.0.0" }));
    var migratedStore = new FileDesktopReleaseStore(legacyOptions);
    Check((await migratedStore.GetActiveAsync(ct))?.RecommendedDesktopVersion == "1.0.0"
        && File.Exists(Path.Combine(legacyOptions.Path, "active.json.legacy")), "Legacy active policy migrated without losing target or source file");
    if (args.Length == 2 && args[0] == "--write-fixture")
    {
        using var fixture = Archive("99.0.1");
        await File.WriteAllBytesAsync(args[1], fixture.ToArray());
        using var secondFixture = Archive("99.0.2");
        await File.WriteAllBytesAsync(args[1] + ".second.zip", secondFixture.ToArray());
        using var thirdFixture = Archive("99.0.3");
        await File.WriteAllBytesAsync(args[1] + ".third.zip", thirdFixture.ToArray());
        Console.WriteLine("Test-only fixture created (not a runnable desktop installer).");
    }
    if (args.Length == 2 && args[0] == "--validate-archive")
    {
        var actual = new FileDesktopReleaseStore(new() { Path = Path.Combine(root, "actual") });
        await using var input = File.OpenRead(args[1]);
        var record = await actual.UploadAsync(input, ct);
        var version = record.Manifest.Version;
        await actual.ActivateAsync(new(version), ct);
        Check((await actual.GetActiveAsync(ct))?.RecommendedDesktopVersion == version, "Real CI archive accepted and activated in isolated storage");
        foreach (var installer in record.Manifest.Installers)
            Check(await actual.GetPublicFileAsync(version, installer.Value, ct) != null, "Real installer available: " + installer.Key);
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
