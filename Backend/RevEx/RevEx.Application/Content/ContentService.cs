using RevEx.Application.Abstractions.Persistence;
using RevEx.Application.Abstractions.Storage;
using RevEx.Domain.Contracts.Content;
using RevEx.Domain.Entities;
using ContentEntity = RevEx.Domain.Entities.Content;

namespace RevEx.Application.Content;

public sealed class ContentService(
    IRepository<ContentEntity> repository,
    IRepository<Category> categoryRepository,
    IRepository<ContentStatus> statusRepository,
    IRepository<Tag> tagRepository,
    IRepository<ContentFile> fileRepository,
    IRepository<ContentVersion> versionRepository,
    IContentHistoryStore historyStore,
    IFileStorage fileStorage,
    IUnitOfWork unitOfWork,
    IContentQueries queries,
    RevEx.Application.Abstractions.Identity.ICurrentUser currentUser)
{
    public async Task<IReadOnlyCollection<ContentDto>> GetAll(CancellationToken cancellationToken)
    {
        var entities = await repository.GetAllAsync(cancellationToken);
        return entities.Select(Map).ToArray();
    }

    public async Task<IReadOnlyCollection<ContentDto>> Query(
        ContentQueryDto query,
        CancellationToken cancellationToken)
    {
        if (query.Offset < 0 || query.Limit is <= 0) throw new ContentOperationException(ContentFailure.Validation, "Offset must be non-negative and Limit must be positive.");
        return await queries.QueryAsync(query, cancellationToken);
    }

    public async Task<ContentDto> GetById(int id, CancellationToken cancellationToken)
    {
        var entity = await repository.GetByIdAsync(id, cancellationToken);
        return entity is null ? throw new ContentOperationException(ContentFailure.NotFound) : Map(entity);
    }

    public async Task<ContentDto> Create(
        CreateContentDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Description))
        {
            throw new ContentOperationException(ContentFailure.Validation, "Name and Description must not be empty.");
        }

        var category = await categoryRepository.GetByIdAsync(request.CategoryId, cancellationToken);
        if (category is null)
        {
            throw new ContentOperationException(ContentFailure.NotFound, $"Category with id {request.CategoryId} was not found.");
        }

        var status = await statusRepository.GetByIdAsync(request.ContentStatusId ?? 1, cancellationToken);
        if (status is null)
        {
            throw new ContentOperationException(ContentFailure.NotFound, $"Content status with id {request.ContentStatusId} was not found.");
        }

        var tags = await ResolveTags(request.TagIds ?? [], cancellationToken);
        if (tags is null)
        {
            throw new ContentOperationException(ContentFailure.NotFound, "One or more tags were not found.");
        }

        var entity = new ContentEntity
        {
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            CategoryId = category.Id,
            Category = category,
            ContentStatusId = status.Id,
            ContentStatus = status,
            TagIds = tags.Select(tag => tag.Id).ToArray(),
            Tags = tags
        };

        await repository.AddAsync(entity, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await historyStore.AddAsync(
            entity.Id,
            $"Контент создан. Название: {Quote(entity.Name)}; описание: {Quote(entity.Description)}; " +
            $"категория: {Quote(category.Name)}; статус: {Quote(status.Name)}; " +
            $"теги: {FormatTags(tags)}.",
            currentUser.Author,
            cancellationToken);

        var result = Map(entity);
        return result;
    }

    public async Task Update(
        int id,
        UpdateContentDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Description))
        {
            throw new ContentOperationException(ContentFailure.Validation, "Name and Description must not be empty.");
        }

        var entity = await repository.GetByIdAsync(id, cancellationToken);
        if (entity is null)
        {
            throw new ContentOperationException(ContentFailure.NotFound);
        }

        var category = await categoryRepository.GetByIdAsync(request.CategoryId, cancellationToken);
        if (category is null)
        {
            throw new ContentOperationException(ContentFailure.NotFound, $"Category with id {request.CategoryId} was not found.");
        }


        var status = await statusRepository.GetByIdAsync(request.ContentStatusId ?? entity.ContentStatusId, cancellationToken);
        if (status is null)
        {
            throw new ContentOperationException(ContentFailure.NotFound, $"Content status with id {request.ContentStatusId} was not found.");
        }

        ICollection<Tag>? tags = null;
        if (request.TagIds is not null)
        {
            tags = await ResolveTags(request.TagIds, cancellationToken);
            if (tags is null)
            {
                throw new ContentOperationException(ContentFailure.NotFound, "One or more tags were not found.");
            }
        }

        var name = request.Name.Trim();
        var description = request.Description.Trim();
        var historyDescription = BuildUpdateHistoryDescription(
            entity,
            name,
            description,
            category,
            status,
            tags);

        entity.Name = name;
        entity.Description = description;
        entity.CategoryId = category.Id;
        entity.Category = category;
        entity.ContentStatusId = status.Id;
        entity.ContentStatus = status;
        if (tags is not null)
        {
            entity.TagIds = tags.Select(tag => tag.Id).ToArray();
            entity.Tags.Clear();
            foreach (var tag in tags)
            {
                entity.Tags.Add(tag);
            }
        }
        await repository.UpdateAsync(entity, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (historyDescription is not null)
        {
            await historyStore.AddAsync(
                entity.Id,
                historyDescription,
                currentUser.Author,
                cancellationToken);
        }
        return;
    }

    public async Task Delete(int id, CancellationToken cancellationToken)
    {
        var entity = await repository.GetByIdAsync(id, cancellationToken);
        if (entity is null)
        {
            throw new ContentOperationException(ContentFailure.NotFound);
        }

        var versions = await versionRepository.GetAllAsync(cancellationToken);
        var ownedFiles = versions
            .Where(version => version.ContentId == id)
            .SelectMany(version => version.Files)
            .ToList();

        if (entity.PreviewFileId is int previewFileId)
        {
            var previewFile = await fileRepository.GetByIdAsync(previewFileId, cancellationToken);
            if (previewFile is not null)
            {
                ownedFiles.Add(previewFile);
            }
        }

        foreach (var file in ownedFiles.DistinctBy(file => file.Id))
        {
            await fileStorage.DeleteAsync(file.Id, cancellationToken);
        }

        if (entity.PreviewFileId is int fileId)
        {
            var previewFile = ownedFiles.FirstOrDefault(file => file.Id == fileId);
            if (previewFile is not null)
            {
                await fileRepository.DeleteAsync(previewFile, cancellationToken);
            }
        }

        await repository.DeleteAsync(entity, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await historyStore.AddAsync(
            id,
            $"Контент удалён. Название: {Quote(entity.Name)}; описание: {Quote(entity.Description)}; " +
            $"категория: {Quote(entity.Category?.Name ?? entity.CategoryId.ToString())}; " +
            $"статус: {Quote(entity.ContentStatus?.Name ?? entity.ContentStatusId.ToString())}; " +
            $"теги: {FormatTags(entity.Tags)}.",
            currentUser.Author,
            cancellationToken);
        return;
    }

    public async Task<ContentDto> UploadPreview(
        int id,
        PreviewUpload request,
        CancellationToken cancellationToken)
    {
        var entity = await repository.GetByIdAsync(id, cancellationToken);
        if (entity is null)
        {
            throw new ContentOperationException(ContentFailure.NotFound);
        }

        if (!PreviewUpload.IsSupported(request))
        {
            throw new ContentOperationException(ContentFailure.BadRequest, "Preview must be a JPEG, PNG, WebP or GIF image no larger than 10 MB.");
        }

        var oldFileId = entity.PreviewFileId;
        var oldPreviewFile = oldFileId is int existingPreviewId
            ? await fileRepository.GetByIdAsync(existingPreviewId, cancellationToken)
            : null;
        var previewFile = new ContentFile
        {
            Url = "pending",
            OriginalFileName = Path.GetFileName(request.FileName),
            Extension = Path.GetExtension(request.FileName).ToLowerInvariant(),
            SizeBytes = request.Length,
            Role = "preview"
        };
        await fileRepository.AddAsync(previewFile, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            await using (var stream = request.OpenReadStream())
            {
                await fileStorage.SaveAsync(previewFile.Id, stream, cancellationToken);
            }

            previewFile.Url = $"/api/content-files/{previewFile.Id}/content";
            entity.PreviewFileId = previewFile.Id;
            entity.PreviewFile = previewFile;
            await fileRepository.UpdateAsync(previewFile, cancellationToken);
            await repository.UpdateAsync(entity, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await fileStorage.DeleteAsync(previewFile.Id, CancellationToken.None);
            await fileRepository.DeleteAsync(previewFile, CancellationToken.None);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
            throw;
        }

        if (oldFileId is int previousId)
        {
            await fileStorage.DeleteAsync(previousId, cancellationToken);
            var oldFile = await fileRepository.GetByIdAsync(previousId, cancellationToken);
            if (oldFile is not null)
            {
                await fileRepository.DeleteAsync(oldFile, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        await historyStore.AddAsync(
            entity.Id,
            $"Изображение контента изменено: {Quote(oldPreviewFile?.OriginalFileName ?? "не задано")} → " +
            $"{Quote(previewFile.OriginalFileName)}.",
            currentUser.Author,
            cancellationToken);

        return Map(entity);
    }

    public async Task DeletePreview(int id, CancellationToken cancellationToken)
    {
        var entity = await repository.GetByIdAsync(id, cancellationToken);
        if (entity is null)
        {
            throw new ContentOperationException(ContentFailure.NotFound);
        }

        if (entity.PreviewFileId is not int fileId)
        {
            return;
        }

        var oldPreviewFile = await fileRepository.GetByIdAsync(fileId, cancellationToken);
        entity.PreviewFileId = null;
        entity.PreviewFile = null;
        await repository.UpdateAsync(entity, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await fileStorage.DeleteAsync(fileId, cancellationToken);
        var file = await fileRepository.GetByIdAsync(fileId, cancellationToken);
        if (file is not null)
        {
            await fileRepository.DeleteAsync(file, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        await historyStore.AddAsync(
            entity.Id,
            $"Изображение контента изменено: {Quote(oldPreviewFile?.OriginalFileName ?? $"файл #{fileId}")} → " +
            $"{Quote("не задано")}.",
            currentUser.Author,
            cancellationToken);

        return;
    }

    private async Task<ICollection<Tag>?> ResolveTags(
        IReadOnlyCollection<int> tagIds,
        CancellationToken cancellationToken)
    {
        var ids = tagIds.Distinct().ToArray();
        var tags = new List<Tag>(ids.Length);
        foreach (var id in ids)
        {
            var tag = await tagRepository.GetByIdAsync(id, cancellationToken);
            if (tag is null)
            {
                return null;
            }

            tags.Add(tag);
        }

        return tags;
    }

    private static string? BuildUpdateHistoryDescription(
        ContentEntity entity,
        string name,
        string description,
        Category category,
        ContentStatus status,
        ICollection<Tag>? tags)
    {
        var changes = new List<string>();

        if (!string.Equals(entity.Name, name, StringComparison.Ordinal))
        {
            changes.Add($"название: {Quote(entity.Name)} → {Quote(name)}");
        }

        if (!string.Equals(entity.Description, description, StringComparison.Ordinal))
        {
            changes.Add($"описание: {Quote(entity.Description)} → {Quote(description)}");
        }

        if (entity.CategoryId != category.Id)
        {
            changes.Add($"категория изменена: {Quote(entity.Category?.Name ?? entity.CategoryId.ToString())} → {Quote(category.Name)}");
        }

        if (entity.ContentStatusId != status.Id)
        {
            changes.Add($"статус: {Quote(entity.ContentStatus?.Name ?? entity.ContentStatusId.ToString())} → {Quote(status.Name)}");
        }

        if (tags is not null)
        {
            var oldTagIds = entity.TagIds.ToHashSet();
            var newTagIds = tags.Select(tag => tag.Id).ToHashSet();
            var addedTags = tags.Where(tag => !oldTagIds.Contains(tag.Id)).Select(tag => tag.Name).OrderBy(name => name).ToArray();
            var removedTags = entity.Tags.Where(tag => !newTagIds.Contains(tag.Id)).Select(tag => tag.Name).OrderBy(name => name).ToArray();

            if (addedTags.Length > 0)
            {
                changes.AddRange(addedTags.Select(tagName => $"добавлен тег {Quote(tagName)}"));
            }

            if (removedTags.Length > 0)
            {
                changes.AddRange(removedTags.Select(tagName => $"удален тег {Quote(tagName)}"));
            }
        }

        return changes.Count == 0 ? null : $"Контент изменён. {string.Join("; ", changes)}.";
    }

    private static string Quote(string value) => $"«{value}»";

    private static string FormatTags(IEnumerable<Tag> tags)
    {
        var names = tags.Select(tag => tag.Name).OrderBy(name => name).Select(Quote).ToArray();
        return names.Length == 0 ? Quote("нет") : string.Join(", ", names);
    }

    private static ContentDto Map(ContentEntity entity) =>
        new(
            entity.Id,
            entity.Name,
            entity.Description,
            entity.CategoryId,
            entity.ContentStatusId,
            entity.PreviewFileId,
            entity.Tags.Select(tag => tag.Id).ToArray(),
            entity.CreatedAt,
            entity.UpdatedAt);
}
