using Microsoft.AspNetCore.Mvc;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Application.DTOs.ContentStatus;
using RevEx.Domain.Entities;
using ContentEntity = RevEx.Domain.Entities.Content;

namespace RevEx.Api.Controllers;

[ApiController]
[Route("api/content-statuses")]
public sealed class ContentStatusesController(
    IRepository<ContentStatus> repository,
    IRepository<ContentEntity> contentRepository,
    IUnitOfWork unitOfWork) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ContentStatusDto>>> GetAll(CancellationToken cancellationToken)
    {
        var entities = await repository.GetAllAsync(cancellationToken);
        return Ok(entities.Select(Map).ToArray());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ContentStatusDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var entity = await repository.GetByIdAsync(id, cancellationToken);
        return entity is null ? NotFound() : Ok(Map(entity));
    }

    [HttpPost]
    public async Task<ActionResult<ContentStatusDto>> Create(
        CreateContentStatusDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ValidationProblem("Name must not be empty.");
        }

        var name = request.Name.Trim();
        var statuses = await repository.GetAllAsync(cancellationToken);
        if (statuses.Any(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            return Conflict(new { message = "A content status with this name already exists." });
        }

        var entity = new ContentStatus { Name = name };
        await repository.AddAsync(entity, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, Map(entity));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        UpdateContentStatusDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ValidationProblem("Name must not be empty.");
        }

        var entity = await repository.GetByIdAsync(id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        var name = request.Name.Trim();
        var statuses = await repository.GetAllAsync(cancellationToken);
        if (statuses.Any(item => item.Id != id && string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            return Conflict(new { message = "A content status with this name already exists." });
        }

        entity.Name = name;
        await repository.UpdateAsync(entity, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var entity = await repository.GetByIdAsync(id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        var contents = await contentRepository.GetAllAsync(cancellationToken);
        if (contents.Any(content => content.ContentStatusId == id))
        {
            return Conflict(new { message = "The status is used by content and cannot be deleted." });
        }

        await repository.DeleteAsync(entity, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static ContentStatusDto Map(ContentStatus entity) =>
        new(entity.Id, entity.Name, entity.CreatedAt, entity.UpdatedAt);
}
