using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using RevEx.Api.Authorization;
using RevEx.Api.Models;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Application.Abstractions.Storage;
using RevEx.Domain.Contracts.ContentVersion;
using RevEx.Domain.Entities;
using ContentEntity = RevEx.Domain.Entities.Content;

namespace RevEx.Api.Controllers;

[ApiController]
[Route("api/content-versions")]
public sealed class ContentVersionsController(
    IRepository<ContentEntity> contentRepository,
    IRepository<ContentVersion> versionRepository,
    IRepository<ContentFile> fileRepository,
    IContentHistoryStore historyStore,
    IUnitOfWork unitOfWork,
    IFileStorage fileStorage) : ControllerBase
{
    private const long MaximumFileSizeBytes = 100 * 1024 * 1024;
    private static readonly IReadOnlyDictionary<string, string[]> AllowedExtensionsByRole =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["primary"] = [".rfa"],
            ["type-catalog"] = [".txt"],
            ["lookup-table"] = [".csv"],
            ["attachment"] = [".txt", ".csv", ".pdf"]
        };

    [HttpGet]
    [ProducesResponseType<IReadOnlyCollection<ContentVersionDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<ContentVersionDto>>> GetAll(
        CancellationToken cancellationToken)
    {
        var entities = await versionRepository.GetAllAsync(cancellationToken);
        return Ok(entities.Select(Map).ToArray());
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<ContentVersionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContentVersionDto>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var entity = await versionRepository.GetByIdAsync(id, cancellationToken);
        return entity is null ? NotFound() : Ok(Map(entity));
    }

    [HttpPost]
    [Authorize(Policy = RevExPolicies.EditContent)]
    [ProducesResponseType<ContentVersionDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContentVersionDto>> Create(
        CreateContentVersionDto request,
        CancellationToken cancellationToken)
    {
        var content = await contentRepository.GetByIdAsync(request.ContentId, cancellationToken);
        if (content is null)
        {
            return NotFound(new { message = $"Content with id {request.ContentId} was not found." });
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
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await historyStore.AddAsync(
            entity.ContentId,
            $"Версия создана. Название: {Quote(entity.Name)}; дата: {Quote(FormatDate(entity.Date))}; " +
            $"статус: {Quote(entity.Status)}; приложение: {Quote(entity.Application)}; " +
            $"версия приложения: {Quote(entity.ApplicationVersion)}.",
            ContentHistoryController.ResolveAuthor(User),
            cancellationToken);

        var result = Map(entity);
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, result);
    }

    [HttpPost("{id:int}/file")]
    [Authorize(Policy = RevExPolicies.EditContent)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<ContentVersionDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContentVersionDto>> AddFile(
        int id,
        [FromForm] UploadFileRequest request,
        CancellationToken cancellationToken)
    {
        var version = await versionRepository.GetByIdAsync(id, cancellationToken);
        if (version is null)
        {
            return NotFound();
        }

        if (request.File.Length == 0 || request.File.Length > MaximumFileSizeBytes)
        {
            return BadRequest(new { message = "The uploaded file must be between 1 byte and 100 MB." });
        }

        var role = request.Role.Trim().ToLowerInvariant();
        var originalFileName = Path.GetFileName(request.File.FileName);
        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(originalFileName) ||
            !AllowedExtensionsByRole.TryGetValue(role, out var allowedExtensions) ||
            !allowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message = "The file extension is not allowed for the selected file role."
            });
        }

        if (role == "primary" && version.Files.Any(item => item.Role == "primary"))
        {
            return Conflict(new { message = "This content version already has a primary Revit file." });
        }

        var file = new ContentFile
        {
            Url = "pending",
            OriginalFileName = originalFileName,
            Extension = extension,
            SizeBytes = request.File.Length,
            Role = role,
            ContentVersionId = version.Id,
            ContentVersion = version
        };
        await fileRepository.AddAsync(file, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            await using var stream = request.File.OpenReadStream();
            await fileStorage.SaveAsync(file.Id, stream, cancellationToken);

            file.Url = $"/api/content-files/{file.Id}/content";

            await fileRepository.UpdateAsync(file, cancellationToken);
            await versionRepository.UpdateAsync(version, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await fileStorage.DeleteAsync(file.Id, CancellationToken.None);
            await fileRepository.DeleteAsync(file, CancellationToken.None);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
            throw;
        }

        await historyStore.AddAsync(
            version.ContentId,
            $"Файл версии {Quote(version.Name)} изменён: {Quote("не задано")} → {Quote(originalFileName)}; " +
            $"роль: {Quote(role)}; размер: {request.File.Length} байт.",
            ContentHistoryController.ResolveAuthor(User),
            cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = version.Id }, Map(version));
    }

    [HttpDelete("{id:int}/files/{fileId:int}")]
    [Authorize(Policy = RevExPolicies.EditContent)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteFile(
        int id,
        int fileId,
        CancellationToken cancellationToken)
    {
        var version = await versionRepository.GetByIdAsync(id, cancellationToken);
        if (version is null)
        {
            return NotFound();
        }

        var file = version.Files.FirstOrDefault(item => item.Id == fileId);
        if (file is null)
        {
            return NotFound(new { message = $"File {fileId} is not attached to content version {id}." });
        }

        version.Files.Remove(file);
        await fileStorage.DeleteAsync(file.Id, cancellationToken);
        await fileRepository.DeleteAsync(file, cancellationToken);
        await versionRepository.UpdateAsync(version, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await historyStore.AddAsync(
            version.ContentId,
            $"Файл версии {Quote(version.Name)} изменён: {Quote(file.OriginalFileName)} → {Quote("удалён")}; " +
            $"роль: {Quote(file.Role)}; размер: {file.SizeBytes} байт.",
            ContentHistoryController.ResolveAuthor(User),
            cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = RevExPolicies.EditContent)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        int id,
        UpdateContentVersionDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Date == default)
        {
            return ValidationProblem("Name and Date must be provided.");
        }

        var version = await versionRepository.GetByIdAsync(id, cancellationToken);
        if (version is null)
        {
            return NotFound();
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
        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (historyDescription is not null)
        {
            await historyStore.AddAsync(
                version.ContentId,
                historyDescription,
                ContentHistoryController.ResolveAuthor(User),
                cancellationToken);
        }
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = RevExPolicies.EditContent)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var version = await versionRepository.GetByIdAsync(id, cancellationToken);
        if (version is null)
        {
            return NotFound();
        }

        foreach (var file in version.Files.ToArray())
        {
            await fileStorage.DeleteAsync(file.Id, cancellationToken);
            await fileRepository.DeleteAsync(file, cancellationToken);
        }

        await versionRepository.DeleteAsync(version, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await historyStore.AddAsync(
            version.ContentId,
            $"Версия удалена. Название: {Quote(version.Name)}; дата: {Quote(FormatDate(version.Date))}; " +
            $"статус: {Quote(version.Status)}; приложение: {Quote(version.Application)}; " +
            $"версия приложения: {Quote(version.ApplicationVersion)}; файлов: {version.Files.Count}.",
            ContentHistoryController.ResolveAuthor(User),
            cancellationToken);
        return NoContent();
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
