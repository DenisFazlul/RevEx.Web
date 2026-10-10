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
        // One-time upgrade of the old single-release policy. Preserve the file for recovery.
        var legacy = System.IO.Path.Combine(_root, "active.json");
        if (File.Exists(legacy))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(legacy));
            var version = document.RootElement.GetProperty("version").GetString()!;
            DesktopVersions.Parse(version);
            var path = System.IO.Path.Combine(_root, version, "record.json");
            if (File.Exists(path))
            {
                var record = JsonSerializer.Deserialize<DesktopReleaseRecord>(File.ReadAllText(path), Json)!;
                var upgraded = record with { PublishedAt = record.PublishedAt ?? record.UploadedAt, IsActive = true };
                var temporary = path + ".migration.tmp";
                File.WriteAllText(temporary, JsonSerializer.Serialize(upgraded, Json));
                File.Move(temporary, path, true);
                File.Move(legacy, legacy + ".legacy", true);
            }
        }
    }

    public async Task<IReadOnlyList<DesktopReleaseRecord>> ListAsync(CancellationToken cancellationToken)
    {
        await _mutation.WaitAsync(cancellationToken);
        try
        {
            return await ListCoreAsync(cancellationToken);
        }
        finally { _mutation.Release(); }
    }

    private async Task<IReadOnlyList<DesktopReleaseRecord>> ListCoreAsync(CancellationToken cancellationToken)
    {
        var result = new List<DesktopReleaseRecord>();
        foreach (var directory in Directory.EnumerateDirectories(_root).Where(p => !System.IO.Path.GetFileName(p).StartsWith('.')))
        {
            var record = await ReadAsync<DesktopReleaseRecord>(System.IO.Path.Combine(directory, "record.json"), cancellationToken);
            if (record != null) result.Add(record);
        }
        var trash = System.IO.Path.Combine(_root, ".trash");
        if (Directory.Exists(trash))
            foreach (var directory in Directory.EnumerateDirectories(trash))
            {
                var record = await ReadAsync<DesktopReleaseRecord>(System.IO.Path.Combine(directory, "record.json"), cancellationToken);
                if (record != null) result.Add(record with { DeletedAt = record.DeletedAt ?? Directory.GetLastWriteTimeUtc(directory) });
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
            if (Directory.Exists(target) || Directory.Exists(System.IO.Path.Combine(_root, ".trash", manifest.Version)))
                throw new InvalidOperationException("Эта версия уже загружена или находится в корзине. Восстановите её либо выпустите новую версию.");
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
        DesktopVersions.Parse(activation.Version);
        await _mutation.WaitAsync(cancellationToken);
        try
        {
            var record = await ReadAsync<DesktopReleaseRecord>(System.IO.Path.Combine(_root, activation.Version, "record.json"), cancellationToken)
                ?? throw new ArgumentException("Релиз не найден.");
            if (!activation.IsActive)
            {
                if (record.PublishedAt == null) throw new ArgumentException("Черновик ещё не опубликован.");
                if (record.IsActive && !(await ListCoreAsync(cancellationToken)).Any(r => r.IsActive && r.DeletedAt == null && r.Manifest.Version != activation.Version))
                    throw new InvalidOperationException("Нельзя отключить последнюю активную версию. Сначала включите другую.");
                await WriteAtomicAsync(System.IO.Path.Combine(_root, activation.Version, "record.json"), record with { IsActive = false }, cancellationToken);
                return;
            }
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
            await WriteAtomicAsync(System.IO.Path.Combine(_root, activation.Version, "record.json"),
                record with { PublishedAt = record.PublishedAt ?? DateTimeOffset.UtcNow, IsActive = true }, cancellationToken);
        }
        finally { _mutation.Release(); }
    }

    public async Task<DesktopReleaseRecord> EditAsync(string version, DesktopReleaseEdit edit, CancellationToken cancellationToken)
    {
        DesktopVersions.Parse(version);
        if (edit.Notes == null || edit.Notes.Length > 12000) throw new ArgumentException("Описание должно быть не длиннее 12000 символов.");
        await _mutation.WaitAsync(cancellationToken);
        try
        {
            var path = System.IO.Path.Combine(_root, version, "record.json");
            var record = await ReadAsync<DesktopReleaseRecord>(path, cancellationToken) ?? throw new ArgumentException("Сборка не найдена или находится в корзине.");
            var updated = record with { Manifest = record.Manifest with { Notes = edit.Notes } };
            await WriteAtomicAsync(path, updated, cancellationToken);
            return updated;
        }
        finally { _mutation.Release(); }
    }

    public async Task DeleteAsync(string version, CancellationToken cancellationToken)
    {
        DesktopVersions.Parse(version);
        await _mutation.WaitAsync(cancellationToken);
        try
        {
            var source = System.IO.Path.Combine(_root, version);
            var record = await ReadAsync<DesktopReleaseRecord>(System.IO.Path.Combine(source, "record.json"), cancellationToken)
                ?? throw new ArgumentException("Сборка не найдена.");
            if (record.PublishedAt != null) throw new InvalidOperationException("Опубликованные пакеты сохраняются для истории и выданных обновлений. Отключите поддержку версии.");
            var trash = System.IO.Path.Combine(_root, ".trash");
            Directory.CreateDirectory(trash);
            var target = System.IO.Path.Combine(trash, version);
            Directory.Move(source, target);
            await WriteAtomicAsync(System.IO.Path.Combine(target, "record.json"), record with { DeletedAt = DateTimeOffset.UtcNow }, CancellationToken.None);
        }
        finally { _mutation.Release(); }
    }

    public async Task RestoreAsync(string version, CancellationToken cancellationToken)
    {
        DesktopVersions.Parse(version);
        await _mutation.WaitAsync(cancellationToken);
        try
        {
            var source = System.IO.Path.Combine(_root, ".trash", version);
            var record = await ReadAsync<DesktopReleaseRecord>(System.IO.Path.Combine(source, "record.json"), cancellationToken)
                ?? throw new ArgumentException("Сборка не найдена в корзине.");
            var target = System.IO.Path.Combine(_root, version);
            if (Directory.Exists(target)) throw new InvalidOperationException("Версия уже существует.");
            await WriteAtomicAsync(System.IO.Path.Combine(source, "record.json"), record with { DeletedAt = null }, cancellationToken);
            Directory.Move(source, target);
        }
        finally { _mutation.Release(); }
    }

    public async Task<DesktopReleaseInfo?> GetActiveAsync(CancellationToken cancellationToken)
    {
        await _mutation.WaitAsync(cancellationToken);
        try
        {
            var record = (await ListCoreAsync(cancellationToken)).FirstOrDefault(r => r.IsActive && r.PublishedAt != null && r.DeletedAt == null);
            if (record == null) return null;
            var path = $"api/desktop-releases/{record.Manifest.Version}/files/";
            return new DesktopReleaseInfo(Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "unknown",
                record.Manifest.Version, path,
                record.Manifest.Installers.ToDictionary(p => p.Key, p => path + p.Value), record.Manifest.Notes);
        }
        finally { _mutation.Release(); }
    }

    public async Task<bool> IsSupportedAsync(string version, CancellationToken cancellationToken)
    {
        DesktopVersions.Parse(version);
        return (await ListAsync(cancellationToken)).Any(r => r.Manifest.Version == version && r.IsActive && r.PublishedAt != null && r.DeletedAt == null);
    }

    public async Task<DesktopPolicy?> CheckAsync(string version, string platform, CancellationToken cancellationToken)
    {
        var current = DesktopVersions.Parse(version);
        if (!Platforms.Contains(platform)) throw new ArgumentException("ОС или архитектура не поддерживается.");
        await _mutation.WaitAsync(cancellationToken);
        try
        {
            var records = await ListCoreAsync(cancellationToken);
            var target = records.FirstOrDefault(r => r.IsActive && r.PublishedAt != null && r.DeletedAt == null);
            if (target == null) return null;
            var targetVersion = target.Manifest.Version;
            if (version == targetVersion) return new("current", targetVersion, target.Manifest.Notes, null, false);
            var supported = records.Any(r => r.Manifest.Version == version && r.IsActive && r.PublishedAt != null && r.DeletedAt == null);
            using var feed = JsonDocument.Parse(await File.ReadAllTextAsync(RequireFile(System.IO.Path.Combine(_root, targetVersion), $"releases.{platform}.json"), cancellationToken));
            var asset = feed.RootElement.GetProperty("Assets")[0];
            var package = new DesktopPackage(targetVersion, platform, asset.GetProperty("FileName").GetString()!,
                asset.GetProperty("Size").GetInt64(), asset.GetProperty("SHA256").GetString()!, asset.TryGetProperty("SHA1", out var sha1) ? sha1.GetString() ?? "" : "");
            return new(supported ? "optional" : "required", targetVersion, target.Manifest.Notes, package, DesktopVersions.Parse(targetVersion) < current);
        }
        finally { _mutation.Release(); }
    }

    public async Task<string?> GetPublishedPackageAsync(string version, string fileName, CancellationToken cancellationToken)
    {
        DesktopVersions.Parse(version);
        if (!SafeName(fileName) || !fileName.EndsWith(".nupkg", StringComparison.Ordinal)) return null;
        var record = (await ListAsync(cancellationToken)).FirstOrDefault(r => r.Manifest.Version == version && r.PublishedAt != null && r.DeletedAt == null);
        if (record == null) return null;
        var path = System.IO.Path.Combine(_root, version, fileName);
        return File.Exists(path) ? path : null;
    }

    public async Task<string?> GetPublicFileAsync(string version, string fileName, CancellationToken cancellationToken)
    {
        DesktopVersions.Parse(version);
        if (!SafeName(fileName)) return null;
        var record = (await ListAsync(cancellationToken)).FirstOrDefault(r => r.Manifest.Version == version && r.PublishedAt != null && r.DeletedAt == null);
        if (record == null) return null;
        var allowed = record.Manifest.Installers.Values.Contains(fileName);
        // Legacy installed clients need their feed and package to acquire the new updater.
        foreach (var platform in Platforms)
        {
            var feedName = $"releases.{platform}.json";
            if (fileName == feedName) allowed = true;
            using var feed = JsonDocument.Parse(await File.ReadAllTextAsync(RequireFile(System.IO.Path.Combine(_root, version), feedName), cancellationToken));
            if (fileName == feed.RootElement.GetProperty("Assets")[0].GetProperty("FileName").GetString()) allowed = true;
        }
        if (!allowed) return null;
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
