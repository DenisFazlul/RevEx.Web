using System.Text.Json;
using System.Text.RegularExpressions;
using RevEx.Application.Abstractions.Storage;
using RevEx.Domain.Contracts.DesktopReleases;

namespace RevEx.Infrastructure.DesktopReleases;

public sealed class FileRevitReleaseStore(DesktopReleaseOptions options) : IRevitReleaseStore
{
    private readonly string root = Path.Combine(Path.GetFullPath(options.Path), ".revit");
    private readonly SemaphoreSlim gate = new(1, 1);
    private static bool ValidId(string id) => Guid.TryParseExact(id, "N", out _);
    private string RecordPath(string id) => Path.Combine(root, id, "record.json");
    public async Task<IReadOnlyList<RevitRelease>> ListAsync(CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try { return await ReadAllAsync(ct); }
        finally { gate.Release(); }
    }
    private async Task<List<RevitRelease>> ReadAllAsync(CancellationToken ct)
    {
        var records = new List<RevitRelease>();
        if (!Directory.Exists(root)) return records;
        foreach (var directory in Directory.EnumerateDirectories(root))
        {
            var path = Path.Combine(directory, "record.json");
            if (File.Exists(path)) records.Add(JsonSerializer.Deserialize<RevitRelease>(await File.ReadAllTextAsync(path, ct))!);
        }
        return records.OrderByDescending(x => x.UploadedAt).ToList();
    }
    public async Task<RevitRelease> UploadAsync(string version, string revitVersion, string notes, string fileName, Stream file, CancellationToken ct)
    {
        DesktopVersions.Parse(version);
        if (!Regex.IsMatch(revitVersion ?? "", @"^20\d{2}$")) throw new ArgumentException("Укажите год версии Revit, например 2025.");
        if ((notes?.Length ?? 0) > 12000) throw new ArgumentException("Описание слишком длинное.");
        fileName = Path.GetFileName(fileName.Replace('\\', '/'));
        if (!new[] { ".exe", ".msi", ".zip" }.Contains(Path.GetExtension(fileName).ToLowerInvariant()))
            throw new ArgumentException("Выберите установщик EXE, MSI или ZIP.");
        await gate.WaitAsync(ct);
        string? directory = null;
        try
        {
            if ((await ReadAllAsync(ct)).Any(x => x.Version == version && x.RevitVersion == revitVersion))
                throw new InvalidOperationException("Такая версия для этого Revit уже загружена.");
            var record = new RevitRelease(Guid.NewGuid().ToString("N"), version, revitVersion!, notes ?? "", fileName, DateTimeOffset.UtcNow, false);
            directory = Path.Combine(root, record.Id);
            Directory.CreateDirectory(directory);
            await using (var output = File.Create(Path.Combine(directory, "installer")))
            {
                var buffer = new byte[81920];
                long total = 0;
                int count;
                while ((count = await file.ReadAsync(buffer, ct)) > 0)
                {
                    total += count;
                    if (total > 1073741824) throw new ArgumentException("Максимальный размер — 1 ГБ.");
                    await output.WriteAsync(buffer.AsMemory(0, count), ct);
                }
                if (total == 0) throw new ArgumentException("Файл пуст.");
            }
            await File.WriteAllTextAsync(RecordPath(record.Id), JsonSerializer.Serialize(record), ct);
            return record;
        }
        catch { if (directory != null) Directory.Delete(directory, true); throw; }
        finally { gate.Release(); }
    }
    public async Task<bool> PublishAsync(string id, bool published, CancellationToken ct)
    {
        if (!ValidId(id)) return false;
        await gate.WaitAsync(ct);
        try
        {
            var path = RecordPath(id);
            if (!File.Exists(path)) return false;
            var record = JsonSerializer.Deserialize<RevitRelease>(await File.ReadAllTextAsync(path, ct))!;
            await File.WriteAllTextAsync(path + ".tmp", JsonSerializer.Serialize(record with { IsPublished = published }), ct);
            File.Move(path + ".tmp", path, true);
            return true;
        }
        finally { gate.Release(); }
    }
    public async Task<(string Path, string Name)?> GetFileAsync(string id, CancellationToken ct)
    {
        var record = (await ListAsync(ct)).FirstOrDefault(x => x.Id == id && x.IsPublished);
        return record == null ? null : (Path.Combine(root, id, "installer"), record.FileName);
    }
}
