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
                var publishedAt = record.PublishedAt ?? record.UploadedAt;
                var upgraded = Normalize(record with { Platforms = record.Manifest.Installers.Keys.ToDictionary(
                    p => p, _ => new DesktopPlatformState(publishedAt, true)) });
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
            if (record != null) result.Add(Normalize(record));
        }
        var trash = System.IO.Path.Combine(_root, ".trash");
        if (Directory.Exists(trash))
            foreach (var directory in Directory.EnumerateDirectories(trash))
            {
                var record = await ReadAsync<DesktopReleaseRecord>(System.IO.Path.Combine(directory, "record.json"), cancellationToken);
                if (record != null) result.Add(Normalize(record with { DeletedAt = record.DeletedAt ?? Directory.GetLastWriteTimeUtc(directory) }));
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
            if (zip.Entries.Count is < 4 or > 32)
                throw new ArgumentException("Архив должен содержать release.json, установщик, feed и полный пакет хотя бы одной платформы (не более 32 файлов).");
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
            if (manifest.Installers.Count == 0 || manifest.Installers.Keys.Any(p => !Platforms.Contains(p)))
                throw new ArgumentException("Укажите хотя бы одну поддерживаемую платформу: win-x64, osx-x64, osx-arm64.");
            var expected = new HashSet<string>(StringComparer.Ordinal) { "release.json" };
            foreach (var platform in manifest.Installers.Keys)
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
                if (!expected.Add(name)) throw new ArgumentException("У каждой платформы должен быть отдельный пакет.");
            }
            if (!expected.SetEquals(names))
                throw new ArgumentException("Архив содержит лишние файлы или неполный комплект заявленных платформ.");
            var target = System.IO.Path.Combine(_root, manifest.Version);
            if (Directory.Exists(System.IO.Path.Combine(_root, ".trash", manifest.Version)))
                throw new InvalidOperationException("Эта версия находится в корзине. Сначала восстановите её.");
            var existing = await ReadAsync<DesktopReleaseRecord>(System.IO.Path.Combine(target, "record.json"), cancellationToken);
            if (existing != null)
            {
                existing = Normalize(existing);
                if (manifest.Installers.Keys.Any(existing.Manifest.Installers.ContainsKey))
                    throw new InvalidOperationException("Эта версия для указанной платформы уже загружена. Замена пакетов запрещена; выпустите новую версию.");
                var installers = new Dictionary<string, string>(existing.Manifest.Installers);
                var states = new Dictionary<string, DesktopPlatformState>(existing.Platforms!);
                foreach (var item in manifest.Installers) { installers.Add(item.Key, item.Value); states.Add(item.Key, new()); }
                var updated = Normalize(existing with { Manifest = existing.Manifest with { Installers = installers }, Platforms = states });
                var addedFiles = expected.Where(n => n != "release.json").ToArray();
                if (addedFiles.Any(n => File.Exists(System.IO.Path.Combine(target, n))))
                    throw new InvalidOperationException("Имена файлов пересекаются с уже загруженной сборкой.");
                var copied = new List<string>();
                try
                {
                    foreach (var name in addedFiles)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var path = System.IO.Path.Combine(target, name);
                        File.Move(System.IO.Path.Combine(staging, name), path);
                        copied.Add(path);
                    }
                    // The record is the commit point: new platforms become visible together.
                    await WriteAtomicAsync(System.IO.Path.Combine(target, "record.json"), updated, cancellationToken);
                }
                catch { foreach (var path in copied) File.Delete(path); throw; }
                return updated;
            }
            if (Directory.Exists(target)) throw new InvalidOperationException("Каталог версии уже существует без метаданных.");
            var record = Normalize(new DesktopReleaseRecord(manifest, DateTimeOffset.UtcNow,
                Platforms: manifest.Installers.Keys.ToDictionary(p => p, _ => new DesktopPlatformState())));
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
            record = Normalize(record);
            var selected = activation.Platform == null ? record.Manifest.Installers.Keys.ToArray() : [activation.Platform];
            if (selected.Any(p => !record.Manifest.Installers.ContainsKey(p)))
                throw new ArgumentException("Сборка для выбранной платформы ещё не загружена.");
            var states = new Dictionary<string, DesktopPlatformState>(record.Platforms!);
            var records = await ListCoreAsync(cancellationToken);
            foreach (var platform in selected)
            {
                var state = states[platform];
                if (!activation.IsActive)
                {
                    if (state.PublishedAt == null) throw new ArgumentException("Сборка для этой платформы ещё не опубликована.");
                    if (state.IsActive && !records.Any(r => r.Manifest.Version != activation.Version && Active(r, platform)))
                        throw new InvalidOperationException($"Нельзя отключить последнюю активную сборку {platform}. Сначала включите другую версию для этой платформы.");
                    states[platform] = state with { IsActive = false };
                    continue;
                }
                var directory = System.IO.Path.Combine(_root, activation.Version);
                RequireFile(directory, record.Manifest.Installers[platform]);
                using var feed = JsonDocument.Parse(await File.ReadAllTextAsync(RequireFile(directory, $"releases.{platform}.json"), cancellationToken));
                var asset = feed.RootElement.GetProperty("Assets")[0];
                var name = asset.GetProperty("FileName").GetString() ?? "";
                if (!SafeName(name)) throw new ArgumentException("Некорректное имя пакета.");
                var path = RequireFile(directory, name);
                await using var stream = File.OpenRead(path);
                var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
                if (!hash.Equals(asset.GetProperty("SHA256").GetString(), StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("Пакет изменён после загрузки. Активация запрещена.");
                states[platform] = new(state.PublishedAt ?? DateTimeOffset.UtcNow, true);
            }
            await WriteAtomicAsync(System.IO.Path.Combine(_root, activation.Version, "record.json"),
                Normalize(record with { Platforms = states }), cancellationToken);
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

    public async Task<DesktopReleaseInfo?> GetActiveAsync(CancellationToken cancellationToken, string? platform = null, bool legacy = false)
    {
        if (platform != null && !Platforms.Contains(platform)) throw new ArgumentException("ОС или архитектура не поддерживается.");
        await _mutation.WaitAsync(cancellationToken);
        try
        {
            var records = await ListCoreAsync(cancellationToken);
            if (legacy && platform == null)
                records = records.Where(r => Platforms.All(p => Active(r, p))).ToArray();
            var targets = new Dictionary<string, DesktopPlatformTarget>();
            foreach (var p in platform == null ? Platforms : [platform])
            {
                var record = records.FirstOrDefault(r => Active(r, p));
                if (record == null) continue;
                var path = $"api/desktop-releases/{record.Manifest.Version}/files/";
                targets[p] = new(record.Manifest.Version, path + record.Manifest.Installers[p], record.Manifest.Notes);
            }
            if (targets.Count == 0) return null;
            var latest = targets.Values.OrderByDescending(t => DesktopVersions.Parse(t.Version)).First();
            return new DesktopReleaseInfo(Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "unknown",
                latest.Version, $"api/desktop-releases/{latest.Version}/files/",
                targets.ToDictionary(p => p.Key, p => p.Value.Installer), latest.Notes, targets);
        }
        finally { _mutation.Release(); }
    }

    public async Task<bool> IsSupportedAsync(string version, CancellationToken cancellationToken, string? platform = null)
    {
        DesktopVersions.Parse(version);
        if (platform != null && !Platforms.Contains(platform)) throw new ArgumentException("ОС или архитектура не поддерживается.");
        var record = (await ListAsync(cancellationToken)).FirstOrDefault(r => r.Manifest.Version == version);
        // Old clients without a platform header must be supported on every supported OS.
        return record != null && (platform == null ? Platforms.All(p => Active(record, p)) : Active(record, platform));
    }

    public async Task<DesktopPolicy?> CheckAsync(string version, string platform, CancellationToken cancellationToken)
    {
        var current = DesktopVersions.Parse(version);
        if (!Platforms.Contains(platform)) throw new ArgumentException("ОС или архитектура не поддерживается.");
        await _mutation.WaitAsync(cancellationToken);
        try
        {
            var records = await ListCoreAsync(cancellationToken);
            var target = records.FirstOrDefault(r => Active(r, platform));
            if (target == null) return null;
            var targetVersion = target.Manifest.Version;
            if (version == targetVersion) return new("current", targetVersion, target.Manifest.Notes, null, false);
            var supported = records.Any(r => r.Manifest.Version == version && Active(r, platform));
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
        var allowed = false;
        foreach (var platform in record.Manifest.Installers.Keys.Where(p => Published(record, p)))
        {
            using var feed = JsonDocument.Parse(await File.ReadAllTextAsync(RequireFile(System.IO.Path.Combine(_root, version), $"releases.{platform}.json"), cancellationToken));
            if (fileName == feed.RootElement.GetProperty("Assets")[0].GetProperty("FileName").GetString()) allowed = true;
        }
        if (!allowed) return null;
        var path = System.IO.Path.Combine(_root, version, fileName);
        return File.Exists(path) ? path : null;
    }

    public async Task<string?> GetPublicFileAsync(string version, string fileName, CancellationToken cancellationToken)
    {
        DesktopVersions.Parse(version);
        if (!SafeName(fileName)) return null;
        var record = (await ListAsync(cancellationToken)).FirstOrDefault(r => r.Manifest.Version == version && r.PublishedAt != null && r.DeletedAt == null);
        if (record == null) return null;
        var allowed = record.Manifest.Installers.Any(p => p.Value == fileName && Published(record, p.Key));
        // Legacy installed clients need their feed and package to acquire the new updater.
        foreach (var platform in record.Manifest.Installers.Keys.Where(p => Published(record, p)))
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

    private static DesktopReleaseRecord Normalize(DesktopReleaseRecord record)
    {
        var states = record.Platforms ?? record.Manifest.Installers.Keys.ToDictionary(p => p,
            _ => new DesktopPlatformState(record.PublishedAt, record.IsActive));
        return record with { Platforms = states, IsActive = states.Values.Any(s => s.IsActive),
            PublishedAt = states.Values.Where(s => s.PublishedAt != null).Select(s => s.PublishedAt).Min() };
    }
    private static bool Published(DesktopReleaseRecord record, string platform)
        => record.DeletedAt == null && record.Manifest.Installers.ContainsKey(platform)
            && Normalize(record).Platforms!.TryGetValue(platform, out var state) && state.PublishedAt != null;
    private static bool Active(DesktopReleaseRecord record, string platform)
        => Published(record, platform) && Normalize(record).Platforms![platform].IsActive;

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
