using Microsoft.AspNetCore.Mvc;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Application.Abstractions.Storage;
using RevEx.Application.DTOs.ContentFile;
using RevEx.Domain.Entities;

namespace RevEx.Api.Controllers;

[ApiController]
[Route("api/content-files")]
public sealed class ContentFilesController(
    IRepository<ContentFile> repository,
    IFileStorage fileStorage) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyCollection<ContentFileDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<ContentFileDto>>> GetAll(
        CancellationToken cancellationToken)
    {
        var entities = await repository.GetAllAsync(cancellationToken);
        return Ok(entities.Select(Map).ToArray());
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<ContentFileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContentFileDto>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var entity = await repository.GetByIdAsync(id, cancellationToken);
        return entity is null ? NotFound() : Ok(Map(entity));
    }

    [HttpGet("{id:int}/content")]
    [ProducesResponseType<FileStreamResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetContent(int id, CancellationToken cancellationToken)
    {
        var entity = await repository.GetByIdAsync(id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        var stream = await fileStorage.OpenReadAsync(id, cancellationToken);
        return stream is null
            ? NotFound()
            : File(stream, "application/octet-stream", $"file-{id}");
    }

    private static ContentFileDto Map(ContentFile entity) =>
        new(entity.Id, entity.Url, entity.ContentVersionId, entity.CreatedAt, entity.UpdatedAt);
}
