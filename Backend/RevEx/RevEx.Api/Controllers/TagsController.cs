using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using RevEx.Api.Authorization;
using RevEx.Domain.Contracts.Tag;
using RevEx.Domain.Entities;

namespace RevEx.Api.Controllers;

[ApiController]
[Route("api/tags")]
public sealed class TagsController(RevEx.Application.Catalog.TagsService service) : ControllerBase
{

    [HttpGet]
    [ProducesResponseType<IReadOnlyCollection<TagDto>>(200)]
    public async Task<ActionResult<IReadOnlyCollection<TagDto>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await service.GetAll(cancellationToken));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<TagDto>(200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<TagDto>> GetById(int id, CancellationToken cancellationToken)
    {
        return Ok(await service.GetById(id, cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = RevExPolicies.AdministerCatalog)]
    [ProducesResponseType<TagDto>(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(409)]
    public async Task<ActionResult<TagDto>> Create(CreateTagDto request, CancellationToken cancellationToken)
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
    public async Task<IActionResult> Update(int id, UpdateTagDto request, CancellationToken cancellationToken)
    {
        await service.Update(id, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = RevExPolicies.AdministerCatalog)]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await service.Delete(id, cancellationToken);
        return NoContent();
    }
}
