using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using RevEx.Api.Authorization;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Application.DTOs.Tag;
using RevEx.Domain.Entities;

namespace RevEx.Api.Controllers;

[ApiController]
[Route("api/tag-groups")]
public sealed class TagGroupsController(
    IRepository<TagGroup> repository,
    IRepository<Tag> tagRepository,
    IUnitOfWork unitOfWork) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<TagGroupDto>>> GetAll(CancellationToken cancellationToken)
    {
        var entities = await repository.GetAllAsync(cancellationToken);
        return Ok(entities.Select(Map).ToArray());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TagGroupDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var entity = await repository.GetByIdAsync(id, cancellationToken);
        return entity is null ? NotFound() : Ok(Map(entity));
    }

    [HttpPost]
    [Authorize(Policy = RevExPolicies.AdministerCatalog)]
    public async Task<ActionResult<TagGroupDto>> Create(CreateTagGroupDto request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name)) return ValidationProblem("Name must not be empty.");
        if (await NameExists(name, null, cancellationToken))
            return Conflict(new { message = "A tag group with this name already exists." });

        var entity = new TagGroup { Name = name };
        await repository.AddAsync(entity, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, Map(entity));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = RevExPolicies.AdministerCatalog)]
    public async Task<IActionResult> Update(int id, UpdateTagGroupDto request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name)) return ValidationProblem("Name must not be empty.");
        var entity = await repository.GetByIdAsync(id, cancellationToken);
        if (entity is null) return NotFound();
        if (await NameExists(name, id, cancellationToken))
            return Conflict(new { message = "A tag group with this name already exists." });

        entity.Name = name;
        await repository.UpdateAsync(entity, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = RevExPolicies.AdministerCatalog)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var entity = await repository.GetByIdAsync(id, cancellationToken);
        if (entity is null) return NotFound();
        var tags = await tagRepository.GetAllAsync(cancellationToken);
        if (tags.Any(tag => tag.TagGroupId == id))
            return Conflict(new { message = "Move or delete all tags in this group first." });

        await repository.DeleteAsync(entity, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<bool> NameExists(string name, int? excludedId, CancellationToken cancellationToken)
    {
        var groups = await repository.GetAllAsync(cancellationToken);
        return groups.Any(item => item.Id != excludedId &&
            string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private static TagGroupDto Map(TagGroup entity) =>
        new(entity.Id, entity.Name, entity.CreatedAt, entity.UpdatedAt);
}
