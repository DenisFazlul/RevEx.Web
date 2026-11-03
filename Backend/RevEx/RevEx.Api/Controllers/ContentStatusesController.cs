using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using RevEx.Api.Authorization;
using RevEx.Domain.Contracts.ContentStatus;
using RevEx.Domain.Entities;
using ContentEntity = RevEx.Domain.Entities.Content;

namespace RevEx.Api.Controllers;

[ApiController]
[Route("api/content-statuses")]
public sealed class ContentStatusesController(RevEx.Application.Catalog.ContentStatusesService service) : ControllerBase
{

    [HttpGet]
    [ProducesResponseType<IReadOnlyCollection<ContentStatusDto>>(200)]
    public async Task<ActionResult<IReadOnlyCollection<ContentStatusDto>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await service.GetAll(cancellationToken));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<ContentStatusDto>(200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<ContentStatusDto>> GetById(int id, CancellationToken cancellationToken)
    {
        return Ok(await service.GetById(id, cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = RevExPolicies.AdministerCatalog)]
    [ProducesResponseType<ContentStatusDto>(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(409)]
    public async Task<ActionResult<ContentStatusDto>> Create(
        CreateContentStatusDto request,
        CancellationToken cancellationToken)
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
    public async Task<IActionResult> Update(
        int id,
        UpdateContentStatusDto request,
        CancellationToken cancellationToken)
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
