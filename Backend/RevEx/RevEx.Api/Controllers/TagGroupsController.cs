using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using RevEx.Api.Authorization;
using RevEx.Domain.Contracts.Tag;
using RevEx.Domain.Entities;

namespace RevEx.Api.Controllers;

[ApiController]
[Route("api/tag-groups")]
public sealed class TagGroupsController(RevEx.Application.Catalog.TagGroupsService service) : ControllerBase
{

    [HttpGet]
    [ProducesResponseType<IReadOnlyCollection<TagGroupDto>>(200)]
    public async Task<ActionResult<IReadOnlyCollection<TagGroupDto>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await service.GetAll(cancellationToken));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<TagGroupDto>(200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<TagGroupDto>> GetById(int id, CancellationToken cancellationToken)
    {
        return Ok(await service.GetById(id, cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = RevExPolicies.AdministerCatalog)]
    [ProducesResponseType<TagGroupDto>(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(409)]
    public async Task<ActionResult<TagGroupDto>> Create(CreateTagGroupDto request, CancellationToken cancellationToken)
    {
        var result = await service.Create(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = RevExPolicies.AdministerCatalog)]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> Update(int id, UpdateTagGroupDto request, CancellationToken cancellationToken)
    {
        await service.Update(id, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = RevExPolicies.AdministerCatalog)]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await service.Delete(id, cancellationToken);
        return NoContent();
    }
}
