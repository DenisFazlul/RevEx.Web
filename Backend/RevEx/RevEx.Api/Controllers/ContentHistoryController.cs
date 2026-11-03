using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RevEx.Api.Authorization;
using RevEx.Domain.Contracts.ContentHistory;
using ContentEntity = RevEx.Domain.Entities.Content;

namespace RevEx.Api.Controllers;

[ApiController]
[Route("api/content/{contentId:int}/history")]
public sealed class ContentHistoryController(RevEx.Application.Catalog.ContentHistoryService service) : ControllerBase
{

    [HttpGet]
    [ProducesResponseType<IReadOnlyCollection<ContentHistoryEventDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<ContentHistoryEventDto>>> Get(
        int contentId,
        CancellationToken cancellationToken)
    {
        return Ok(await service.Get(contentId, cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = RevExPolicies.EditContent)]
    [ProducesResponseType<ContentHistoryEventDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContentHistoryEventDto>> Create(
        int contentId,
        CreateContentHistoryEventDto request,
        CancellationToken cancellationToken)
    {
        var result = await service.Create(contentId, request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { contentId }, result);
    }
}
