using RevEx.Application.Abstractions.Persistence;
using RevEx.Application.Abstractions.Storage;
using RevEx.Domain.Contracts.ContentVersion;
using RevEx.Domain.Entities;
using ContentEntity = RevEx.Domain.Entities.Content;
using System.Text.Json;

namespace RevEx.Application.Catalog;
using RevEx.Application.Content;

public sealed class ContentVersionsService(RevEx.Application.Abstractions.Identity.ICurrentUser currentUser,

    IContentRepository contentRepository,
    IContentVersionRepository versionRepository,
    IContentFileRepository fileRepository,
    IContentHistoryStore historyStore,
    IFileStorage fileStorage)
{
    private const long MaximumFileSizeBytes = 100 * 1024 * 1024;
    private static readonly IReadOnlyDictionary<string, string[]> AllowedExtensionsByRole =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["type-catalog"] = [".txt"],
            ["lookup-table"] = [".csv"]
        };

    public async Task<IReadOnlyCollection<ContentVersionDto>> GetAll(
        CancellationToken cancellationToken)
    {
        var entities = await versionRepository.GetAllAsync(cancellationToken);
        return entities.Select(Map).ToArray();
    }

    public async Task<ContentVersionDto> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var entity = await versionRepository.GetByIdAsync(id, cancellationToken);
        return entity is null ? throw new ContentOperationException(ContentFailure.NotFound) : Map(entity);
    }

    public async Task<ContentVersionDto> Create(
        CreateContentVersionDto request,
        CancellationToken cancellationToken)
    {
        var content = await contentRepository.GetByIdAsync(request.ContentId, cancellationToken);
        if (content is null)
        {
            throw new ContentOperationException(ContentFailure.NotFound, $"Content with id {request.ContentId} was not found.");
        }

        var entity = new ContentVersion
        {
            ContentId = content.Id,
            Content = content,
            Name = string.IsNullOrWhiteSpace(request.Name) ? "Новая версия" : request.Name.Trim(),
            Date = request.Date ?? DateTimeOffset.UtcNow,
            Status = request.Status ?? "draft",
            Application = request.Application.Trim().ToLowerInvariant(),
            ApplicationVersion = request.ApplicationVersion.Trim()
        };

        await versionRepository.AddAsync(entity, cancellationToken);

        await historyStore.AddAsync(
            entity.ContentId,
            $"Версия создана. Название: {Quote(entity.Name)}; дата: {Quote(FormatDate(entity.Date))}; " +
            $"статус: {Quote(entity.Status)}; приложение: {Quote(entity.Application)}; " +
            $"версия приложения: {Quote(entity.ApplicationVersion)}.",
            currentUser.Author,
            cancellationToken);

        var result = Map(entity);
        return result;
    }

    public async Task<ContentVersionDto> AddFile(
        int id,
        VersionFileUpload request,
        CancellationToken cancellationToken)
    {
        var version = await versionRepository.GetByIdAsync(id, cancellationToken);
        if (version is null)
        {
            throw new ContentOperationException(ContentFailure.NotFound);
        }

        if (request.Length == 0 || request.Length > MaximumFileSizeBytes)
        {
            throw new ContentOperationException(ContentFailure.BadRequest, "The uploaded file must be between 1 byte and 100 MB.");
        }

        var role = request.Role.Trim().ToLowerInvariant();
        var originalFileName = Path.GetFileName(request.FileName);
        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(originalFileName) ||
            (role != "primary" && role != "attachment" &&
             (!AllowedExtensionsByRole.TryGetValue(role, out var allowedExtensions) ||
              !allowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))))
        {
            throw new ContentOperationException(ContentFailure.BadRequest, "The file extension is not allowed for the selected file role.");
        }

        if (role == "primary" && version.Files.Any(item => item.Role == "primary"))
        {
            throw new ContentOperationException(ContentFailure.Conflict, "This content version already has a primary file.");
        }

        var metadataJson = ValidateMetadata(request.MetadataJson);
        var file = new ContentFile
        {
            Url = "pending",
            OriginalFileName = originalFileName,
            Extension = extension,
            SizeBytes = request.Length,
            Role = role,
            MetadataJson = metadataJson,
            ContentVersionId = version.Id,
            ContentVersion = version
        };
        await fileRepository.AddAsync(file, cancellationToken);
        version.Files.Add(file);

        try
        {
            await using var stream = request.OpenReadStream();
            await fileStorage.SaveAsync(file.Id, stream, cancellationToken);

            file.Url = $"/api/content-files/{file.Id}/content";

            await fileRepository.UpdateAsync(file, cancellationToken);
            await versionRepository.UpdateAsync(version, cancellationToken);

        }
        catch
        {
            await fileStorage.DeleteAsync(file.Id, CancellationToken.None);
            await fileRepository.DeleteAsync(file, CancellationToken.None);

            throw;
        }

        await historyStore.AddAsync(
            version.ContentId,
            $"Файл версии {Quote(version.Name)} изменён: {Quote("не задано")} → {Quote(originalFileName)}; " +
            $"роль: {Quote(role)}; размер: {request.Length} байт.",
            currentUser.Author,
            cancellationToken);

        return Map(version);
    }

    private static string? ValidateMetadata(string? json)
    {
        if (json is null)
        {
            return null;
        }

        if (json.Length > 8 * 1024 * 1024)
        {
            throw new ContentOperationException(ContentFailure.BadRequest, "File metadata exceeds 8 MiB.");
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new ContentOperationException(ContentFailure.BadRequest, "File metadata must be a JSON object.");
            }

            return document.RootElement.GetRawText();
        }
        catch (JsonException)
        {
            throw new ContentOperationException(ContentFailure.BadRequest, "File metadata is not valid JSON.");
        }
    }

    public async Task DeleteFile(
        int id,
        int fileId,
        CancellationToken cancellationToken)
    {
        var version = await versionRepository.GetByIdAsync(id, cancellationToken);
        if (version is null)
        {
            throw new ContentOperationException(ContentFailure.NotFound);
        }

        var file = version.Files.FirstOrDefault(item => item.Id == fileId);
        if (file is null)
        {
            throw new ContentOperationException(ContentFailure.NotFound, $"File {fileId} is not attached to content version {id}.");
        }

        version.Files.Remove(file);
        await fileStorage.DeleteAsync(file.Id, cancellationToken);
        await fileRepository.DeleteAsync(file, cancellationToken);
        await versionRepository.UpdateAsync(version, cancellationToken);

        await historyStore.AddAsync(
            version.ContentId,
            $"Файл версии {Quote(version.Name)} изменён: {Quote(file.OriginalFileName)} → {Quote("удалён")}; " +
            $"роль: {Quote(file.Role)}; размер: {file.SizeBytes} байт.",
            currentUser.Author,
            cancellationToken);
        return;
    }

    public async Task Update(
        int id,
        UpdateContentVersionDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Date == default)
        {
            throw new ContentOperationException(ContentFailure.Validation, "Name and Date must be provided.");
        }

        var version = await versionRepository.GetByIdAsync(id, cancellationToken);
        if (version is null)
        {
            throw new ContentOperationException(ContentFailure.NotFound);
        }

        var name = request.Name.Trim();
        var application = request.Application.Trim().ToLowerInvariant();
        var applicationVersion = request.ApplicationVersion.Trim();
        var historyDescription = BuildUpdateHistoryDescription(
            version,
            name,
            request.Date,
            request.Status,
            application,
            applicationVersion);

        version.Name = name;
        version.Date = request.Date;
        version.Status = request.Status;
        version.Application = application;
        version.ApplicationVersion = applicationVersion;
        await versionRepository.UpdateAsync(version, cancellationToken);

        if (historyDescription is not null)
        {
            await historyStore.AddAsync(
                version.ContentId,
                historyDescription,
                currentUser.Author,
                cancellationToken);
        }
        return;
    }

    public async Task Delete(int id, CancellationToken cancellationToken)
    {
        var version = await versionRepository.GetByIdAsync(id, cancellationToken);
        if (version is null)
        {
            throw new ContentOperationException(ContentFailure.NotFound);
        }

        foreach (var file in version.Files.ToArray())
        {
            await fileStorage.DeleteAsync(file.Id, cancellationToken);
            await fileRepository.DeleteAsync(file, cancellationToken);
        }

        await versionRepository.DeleteAsync(version, cancellationToken);

        await historyStore.AddAsync(
            version.ContentId,
            $"Версия удалена. Название: {Quote(version.Name)}; дата: {Quote(FormatDate(version.Date))}; " +
            $"статус: {Quote(version.Status)}; приложение: {Quote(version.Application)}; " +
            $"версия приложения: {Quote(version.ApplicationVersion)}; файлов: {version.Files.Count}.",
            currentUser.Author,
            cancellationToken);
        return;
    }

    private static string? BuildUpdateHistoryDescription(
        ContentVersion version,
        string name,
        DateTimeOffset date,
        string status,
        string application,
        string applicationVersion)
    {
        var changes = new List<string>();
        AddChange(changes, "название", version.Name, name);

        if (version.Date != date)
            changes.Add($"дата: {Quote(FormatDate(version.Date))} → {Quote(FormatDate(date))}");

        AddChange(changes, "статус", version.Status, status);
        AddChange(changes, "приложение", version.Application, application);
        AddChange(changes, "версия приложения", version.ApplicationVersion, applicationVersion);

        return changes.Count == 0
            ? null
            : $"Версия {Quote(version.Name)} изменена. {string.Join("; ", changes)}.";
    }

    private static void AddChange(ICollection<string> changes, string field, string oldValue, string newValue)
    {
        if (!string.Equals(oldValue, newValue, StringComparison.Ordinal))
            changes.Add($"{field}: {Quote(oldValue)} → {Quote(newValue)}");
    }

    private static string FormatDate(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("dd.MM.yyyy HH:mm:ss 'UTC'");

    private static string Quote(string value) => $"«{value}»";

    private static ContentVersionDto Map(ContentVersion entity) =>
        new(
            entity.Id,
            entity.ContentId,
            entity.Files.Select(file => file.Id).ToArray(),
            entity.Name,
            entity.Date,
            entity.Status,
            entity.Application,
            entity.ApplicationVersion,
            entity.CreatedAt,
            entity.UpdatedAt);
}
