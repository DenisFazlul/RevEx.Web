using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using RevEx.Api.Authorization;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Domain.Contracts.Tag;
using RevEx.Domain.Entities;

namespace RevEx.Api.Controllers;

[ApiController]
[Route("api/tags")]
public sealed class TagsController(
    IRepository<Tag> repository,
    IRepository<TagGroup> groupRepository,
    IUnitOfWork unitOfWork) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyCollection<TagDto>>(200)]
    public async Task<ActionResult<IReadOnlyCollection<TagDto>>> GetAll(CancellationToken cancellationToken)
    {
        var entities = await repository.GetAllAsync(cancellationToken);
        return Ok(entities.Select(Map).ToArray());
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<TagDto>(200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<TagDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var entity = await repository.GetByIdAsync(id, cancellationToken);
        return entity is null ? NotFound() : Ok(Map(entity));
    }

    [HttpPost]
    [Authorize(Policy = RevExPolicies.AdministerCatalog)]
    [ProducesResponseType<TagDto>(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(409)]
    public async Task<ActionResult<TagDto>> Create(CreateTagDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ValidationProblem("Name must not be empty.");
        }

        var name = request.Name.Trim();
        if (await groupRepository.GetByIdAsync(request.TagGroupId, cancellationToken) is null)
        {
            return ValidationProblem("TagGroupId must reference an existing tag group.");
        }
        if (await NameExists(name, null, cancellationToken))
        {
            return Conflict(new { message = "A tag with this name already exists." });
        }

        var entity = new Tag { Name = name, TagGroupId = request.TagGroupId };
        await repository.AddAsync(entity, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, Map(entity));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = RevExPolicies.AdministerCatalog)]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> Update(int id, UpdateTagDto request, CancellationToken cancellationToken)
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
        if (await groupRepository.GetByIdAsync(request.TagGroupId, cancellationToken) is null)
        {
            return ValidationProblem("TagGroupId must reference an existing tag group.");
        }
        if (await NameExists(name, id, cancellationToken))
        {
            return Conflict(new { message = "A tag with this name already exists." });
        }

        entity.Name = name;
        entity.TagGroupId = request.TagGroupId;
        await repository.UpdateAsync(entity, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = RevExPolicies.AdministerCatalog)]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var entity = await repository.GetByIdAsync(id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        await repository.DeleteAsync(entity, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<bool> NameExists(string name, int? excludedId, CancellationToken cancellationToken)
    {
        var tags = await repository.GetAllAsync(cancellationToken);
        return tags.Any(item => item.Id != excludedId && string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private static TagDto Map(Tag entity) =>
        new(entity.Id, entity.Name, entity.TagGroupId, entity.TagGroup?.Name ?? string.Empty,
            entity.CreatedAt, entity.UpdatedAt);
}
