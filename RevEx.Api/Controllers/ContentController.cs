using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using RevEx.Api.Authorization;
using RevEx.Api.Models;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Application.Abstractions.Storage;
using RevEx.Application.DTOs.Content;
using RevEx.Domain.Entities;
using ContentEntity = RevEx.Domain.Entities.Content;

namespace RevEx.Api.Controllers;

[ApiController]
[Route("api/content")]
public sealed class ContentController(
    IRepository<ContentEntity> repository,
    IRepository<Category> categoryRepository,
    IRepository<ContentStatus> statusRepository,
    IRepository<Tag> tagRepository,
    IRepository<ContentFile> fileRepository,
    IFileStorage fileStorage,
    IUnitOfWork unitOfWork) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyCollection<ContentDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<ContentDto>>> GetAll(CancellationToken cancellationToken)
    {
        var entities = await repository.GetAllAsync(cancellationToken);
        return Ok(entities.Select(Map).ToArray());
    }

    [HttpGet("query")]
    [ProducesResponseType<IReadOnlyCollection<ContentDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<ContentDto>>> Query(
        [FromQuery] ContentQueryDto query,
        CancellationToken cancellationToken)
    {
        var entities = await repository.GetAllAsync(cancellationToken);
        var result = entities.AsEnumerable();

        if (query.CategoryIds.Length > 0)
        {
            var categoryIds = query.CategoryIds.ToHashSet();
            result = result.Where(content => categoryIds.Contains(content.CategoryId));
        }

        if (query.TagIds.Length > 0)
        {
            var tagIds = query.TagIds.ToHashSet();
            result = result.Where(content =>
                tagIds.All(tagId => content.Tags.Any(tag => tag.Id == tagId)));
        }

        if (!string.IsNullOrWhiteSpace(query.Name))
        {
            var name = query.Name.Trim();
            result = result.Where(content =>
                content.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
        }

        return Ok(result.Select(Map).ToArray());
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<ContentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContentDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var entity = await repository.GetByIdAsync(id, cancellationToken);
        return entity is null ? NotFound() : Ok(Map(entity));
    }

    [HttpPost]
    [Authorize(Policy = RevExPolicies.EditContent)]
    [ProducesResponseType<ContentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContentDto>> Create(
        CreateContentDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Description))
        {
            return ValidationProblem("Name and Description must not be empty.");
        }

        var category = await categoryRepository.GetByIdAsync(request.CategoryId, cancellationToken);
        if (category is null)
        {
            return NotFound(new { message = $"Category with id {request.CategoryId} was not found." });
        }

        var status = await statusRepository.GetByIdAsync(request.ContentStatusId ?? 1, cancellationToken);
        if (status is null)
        {
            return NotFound(new { message = $"Content status with id {request.ContentStatusId} was not found." });
        }

        var tags = await ResolveTags(request.TagIds ?? [], cancellationToken);
        if (tags is null)
        {
            return NotFound(new { message = "One or more tags were not found." });
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

        var result = Map(entity);
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = RevExPolicies.EditContent)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        int id,
        UpdateContentDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Description))
        {
            return ValidationProblem("Name and Description must not be empty.");
        }

        var entity = await repository.GetByIdAsync(id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        var category = await categoryRepository.GetByIdAsync(request.CategoryId, cancellationToken);
        if (category is null)
        {
            return NotFound(new { message = $"Category with id {request.CategoryId} was not found." });
        }


        var status = await statusRepository.GetByIdAsync(request.ContentStatusId ?? entity.ContentStatusId, cancellationToken);
        if (status is null)
        {
            return NotFound(new { message = $"Content status with id {request.ContentStatusId} was not found." });
        }

        ICollection<Tag>? tags = null;
        if (request.TagIds is not null)
        {
            tags = await ResolveTags(request.TagIds, cancellationToken);
            if (tags is null)
            {
                return NotFound(new { message = "One or more tags were not found." });
            }
        }

        entity.Name = request.Name.Trim();
        entity.Description = request.Description.Trim();
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
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = RevExPolicies.EditContent)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var entity = await repository.GetByIdAsync(id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        if (entity.PreviewFileId is int previewFileId)
        {
            await fileStorage.DeleteAsync(previewFileId, cancellationToken);
            var previewFile = await fileRepository.GetByIdAsync(previewFileId, cancellationToken);
            if (previewFile is not null)
            {
                await fileRepository.DeleteAsync(previewFile, cancellationToken);
            }
        }

        await repository.DeleteAsync(entity, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/preview")]
    [Authorize(Policy = RevExPolicies.EditContent)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<ContentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContentDto>> UploadPreview(
        int id,
        [FromForm] UploadContentPreviewRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await repository.GetByIdAsync(id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        if (!ContentPreviewUploadConfiguration.IsSupported(request.File))
        {
            return BadRequest(new
            {
                message = "Preview must be a JPEG, PNG, WebP or GIF image no larger than 10 MB."
            });
        }

        var oldFileId = entity.PreviewFileId;
        var previewFile = new ContentFile
        {
            Url = "pending",
            OriginalFileName = Path.GetFileName(request.File.FileName),
            Extension = Path.GetExtension(request.File.FileName).ToLowerInvariant(),
            SizeBytes = request.File.Length,
            Role = "preview"
        };
        await fileRepository.AddAsync(previewFile, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await using (var stream = request.File.OpenReadStream())
        {
            await fileStorage.SaveAsync(previewFile.Id, stream, cancellationToken);
        }

        previewFile.Url = $"/api/content-files/{previewFile.Id}/content";
        entity.PreviewFileId = previewFile.Id;
        entity.PreviewFile = previewFile;
        await fileRepository.UpdateAsync(previewFile, cancellationToken);
        await repository.UpdateAsync(entity, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

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

        return Ok(Map(entity));
    }

    [HttpDelete("{id:int}/preview")]
    [Authorize(Policy = RevExPolicies.EditContent)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePreview(int id, CancellationToken cancellationToken)
    {
        var entity = await repository.GetByIdAsync(id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        if (entity.PreviewFileId is not int fileId)
        {
            return NoContent();
        }

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

        return NoContent();
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
