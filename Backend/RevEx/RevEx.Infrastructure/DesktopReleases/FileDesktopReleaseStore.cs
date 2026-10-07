using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using RevEx.Application.Abstractions.Storage;
using RevEx.Application.DTOs.DesktopReleases;

namespace RevEx.Infrastructure.DesktopReleases;

public sealed class FileDesktopReleaseStore : IDesktopReleaseStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly string[] Platforms = ["win-x64", "osx-x64", "osx-arm64"];
    private readonly string _root;
    private readonly long _maximumBytes;
    private readonly SemaphoreSlim _mutation = new(1, 1);

    public FileDesktopReleaseStore(DesktopReleaseOptions options)
    {
        _root = System.IO.Path.GetFullPath(options.Path);
        _maximumBytes = options.MaximumExpandedBytes;
        Directory.CreateDirectory(_root);
    }

    public async Task<IReadOnlyList<DesktopReleaseRecord>> ListAsync(CancellationToken cancellationToken)
    {
        var result = new List<DesktopReleaseRecord>();
        foreach (var directory in Directory.EnumerateDirectories(_root).Where(p => !System.IO.Path.GetFileName(p).StartsWith('.')))
        {
            var record = await ReadAsync<DesktopReleaseRecord>(System.IO.Path.Combine(directory, "record.json"), cancellationToken);
            if (record != null) result.Add(record);
        }
        return result.OrderByDescending(r => DesktopVersions.Parse(r.Manifest.Version)).ToArray();
    }

    public async Task<DesktopReleaseRecord> UploadAsync(Stream archive, CancellationToken cancellationToken)
    {
        await _mutation.WaitAsync(cancellationToken);
        var staging = System.IO.Path.Combine(_root, ".upload-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(staging);
            using var zip = new ZipArchive(archive, ZipArchiveMode.Read, leaveOpen: true);
            if (zip.Entries.Count is < 7 or > 32)
                throw new ArgumentException("Архив должен содержать release.json и полный комплект файлов трёх платформ (не более 32 файлов).");
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            long total = 0;
            foreach (var entry in zip.Entries)
            {
                if (!SafeName(entry.FullName) || !names.Add(entry.FullName) || entry.FullName.Equals("record.json", StringComparison.OrdinalIgnoreCase)
                    || ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000)
                    throw new ArgumentException("Архив должен содержать только уникальные обычные файлы в корне, без путей и ссылок.");
                if (entry.Length < 0 || entry.Length > _maximumBytes - total)
                    throw new ArgumentException("Превышен допустимый размер распакованного релиза.");
                if (entry.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && entry.Length > 65536)
                    throw new ArgumentException("Файл метаданных превышает 64 КБ.");
                await using var input = entry.Open();
                await using var output = File.Create(System.IO.Path.Combine(staging, entry.FullName));
                var buffer = new byte[81920];
                long written = 0;
                int read;
                while ((read = await input.ReadAsync(buffer, cancellationToken)) != 0)
                {
                    written += read;
                    total += read;
                    if (written > entry.Length || total > _maximumBytes) throw new ArgumentException("Превышен допустимый размер архива.");
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
                if (written != entry.Length) throw new ArgumentException("Повреждённый архив.");
            }
            var manifest = await ReadAsync<DesktopReleaseManifest>(System.IO.Path.Combine(staging, "release.json"), cancellationToken)
                ?? throw new ArgumentException("Отсутствует release.json.");
            DesktopVersions.Parse(manifest.Version);
            if (manifest.Notes == null || manifest.Notes.Length > 12000 || manifest.Installers == null)
                throw new ArgumentException("Некорректное описание релиза.");
            var expected = new HashSet<string>(StringComparer.Ordinal) { "release.json" };
            foreach (var platform in Platforms)
            {
                if (!manifest.Installers.TryGetValue(platform, out var installer) || !SafeName(installer)
                    || !installer.EndsWith(platform == "win-x64" ? ".exe" : ".pkg", StringComparison.Ordinal))
                    throw new ArgumentException($"Нет корректного установщика {platform}.");
                var installerPath = RequireFile(staging, installer);
                if (new FileInfo(installerPath).Length == 0 || !expected.Add(installer))
                    throw new ArgumentException("У каждой платформы должен быть отдельный непустой установщик.");
                var feedName = $"releases.{platform}.json";
                using var feed = JsonDocument.Parse(await File.ReadAllTextAsync(RequireFile(staging, feedName), cancellationToken));
                if (!feed.RootElement.TryGetProperty("Assets", out var assetsElement) || assetsElement.ValueKind != JsonValueKind.Array)
                    throw new ArgumentException("Список пакетов должен содержать массив Assets.");
                var assets = assetsElement.EnumerateArray().ToArray();
                if (assets.Length != 1) throw new ArgumentException($"Список {platform} должен содержать один полный пакет.");
                var asset = assets[0];
                if (asset.GetProperty("Type").GetString() != "Full" || asset.GetProperty("Version").GetString() != manifest.Version
                    || asset.GetProperty("PackageId").GetString() != "RevEx.Desktop")
                    throw new ArgumentException($"Пакет {platform} не соответствует версии или идентификатору приложения.");
                var name = asset.GetProperty("FileName").GetString() ?? "";
                if (!SafeName(name) || !name.EndsWith(".nupkg", StringComparison.Ordinal)) throw new ArgumentException("Некорректное имя пакета.");
                var package = RequireFile(staging, name);
                if (new FileInfo(package).Length != asset.GetProperty("Size").GetInt64()) throw new ArgumentException("Размер пакета не совпадает со списком.");
                await using var stream = File.OpenRead(package);
                var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
                if (!hash.Equals(asset.GetProperty("SHA256").GetString(), StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("Контрольная сумма пакета не совпадает.");
                ValidatePackageMetadata(package, manifest.Version, platform);
                expected.Add(feedName);
                expected.Add(name);
            }
            if (manifest.Installers.Count != Platforms.Length || !expected.SetEquals(names))
                throw new ArgumentException("Архив содержит лишние файлы или неполный комплект.");
            var target = System.IO.Path.Combine(_root, manifest.Version);
            if (Directory.Exists(target)) throw new InvalidOperationException("Эта версия уже загружена. Выпустите новую версию.");
            var record = new DesktopReleaseRecord(manifest, DateTimeOffset.UtcNow);
            await WriteAtomicAsync(System.IO.Path.Combine(staging, "record.json"), record, cancellationToken);
            Directory.Move(staging, target);
            return record;
        }
        catch (Exception exception) when (exception is InvalidDataException or JsonException or KeyNotFoundException or FormatException or XmlException)
        {
            throw new ArgumentException("Некорректный архив релиза или список пакетов.", exception);
        }
        finally
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
            _mutation.Release();
        }
    }

    public async Task ActivateAsync(DesktopReleaseActivation activation, CancellationToken cancellationToken)
    {
        var recommended = DesktopVersions.Parse(activation.Version);
        var minimum = DesktopVersions.Parse(activation.MinimumDesktopVersion);
        var maximum = DesktopVersions.Parse(activation.MaximumDesktopVersion);
        if (minimum > recommended || recommended > maximum) throw new ArgumentException("Рекомендуемая версия должна находиться внутри диапазона совместимости.");
        await _mutation.WaitAsync(cancellationToken);
        try
        {
            var record = await ReadAsync<DesktopReleaseRecord>(System.IO.Path.Combine(_root, activation.Version, "record.json"), cancellationToken)
                ?? throw new ArgumentException("Релиз не найден.");
            foreach (var file in record.Manifest.Installers.Values) RequireFile(System.IO.Path.Combine(_root, activation.Version), file);
            foreach (var platform in Platforms)
            {
                var directory = System.IO.Path.Combine(_root, activation.Version);
                using var feed = JsonDocument.Parse(await File.ReadAllTextAsync(RequireFile(directory, $"releases.{platform}.json"), cancellationToken));
                var asset = feed.RootElement.GetProperty("Assets")[0];
                var name = asset.GetProperty("FileName").GetString() ?? "";
                if (!SafeName(name)) throw new ArgumentException("Некорректное имя пакета.");
                var path = RequireFile(directory, name);
                await using var stream = File.OpenRead(path);
                var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
                if (!hash.Equals(asset.GetProperty("SHA256").GetString(), StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("Пакет изменён после загрузки. Активация запрещена.");
            }
            await WriteAtomicAsync(System.IO.Path.Combine(_root, "active.json"), activation, cancellationToken);
        }
        finally { _mutation.Release(); }
    }

    public async Task<DesktopReleaseInfo?> GetActiveAsync(CancellationToken cancellationToken)
    {
        var active = await ReadAsync<DesktopReleaseActivation>(System.IO.Path.Combine(_root, "active.json"), cancellationToken);
        if (active == null) return null;
        var record = await ReadAsync<DesktopReleaseRecord>(System.IO.Path.Combine(_root, active.Version, "record.json"), cancellationToken)
            ?? throw new InvalidOperationException("Файлы активного релиза отсутствуют.");
        var path = $"api/desktop-releases/{active.Version}/files/";
        return new DesktopReleaseInfo(Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "unknown",
            active.Version, active.MinimumDesktopVersion, active.MaximumDesktopVersion, path,
            record.Manifest.Installers.ToDictionary(p => p.Key, p => path + p.Value), record.Manifest.Notes);
    }

    public async Task<string?> GetPublicFileAsync(string version, string fileName, CancellationToken cancellationToken)
    {
        if (!SafeName(fileName) || fileName is "record.json" or "release.json") return null;
        var active = await GetActiveAsync(cancellationToken);
        if (active == null || active.RecommendedDesktopVersion != version) return null;
        var path = System.IO.Path.Combine(_root, version, fileName);
        return File.Exists(path) ? path : null;
    }

    private static bool SafeName(string name) => !string.IsNullOrWhiteSpace(name) && name.Length <= 180
        && name[0] != '.' && name.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_');
    private static void ValidatePackageMetadata(string path, string version, string platform)
    {
        using var package = ZipFile.OpenRead(path);
        var specifications = package.Entries.Where(e => e.FullName.EndsWith(".nuspec", StringComparison.Ordinal)).ToArray();
        if (specifications.Length != 1 || specifications[0].Length > 65536)
            throw new ArgumentException("Пакет не содержит корректную спецификацию Velopack.");
        using var input = specifications[0].Open();
        using var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, MaxCharactersInDocument = 65536 });
        var document = XDocument.Load(reader);
                var metadata = document.Root?.Elements().FirstOrDefault(e => e.Name.LocalName == "metadata");
        string? Get(string name) => metadata?.Elements().SingleOrDefault(e => e.Name.LocalName == name)?.Value;
        if (Get("id") != "RevEx.Desktop" || Get("version") != version || Get("channel") != platform || Get("rid") != platform)
            throw new ArgumentException("Версия, архитектура или канал внутри пакета не соответствует релизу.");
    }
    private static string RequireFile(string root, string name) => File.Exists(System.IO.Path.Combine(root, name))
        ? System.IO.Path.Combine(root, name) : throw new ArgumentException($"Отсутствует файл {name}.");
    private static async Task<T?> ReadAsync<T>(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) return default;
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        return await JsonSerializer.DeserializeAsync<T>(stream, Json, cancellationToken);
    }
    private static async Task WriteAtomicAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(value, Json), cancellationToken);
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
