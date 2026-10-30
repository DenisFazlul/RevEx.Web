using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RevEx.Api.Authorization;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Domain.Contracts.ContentHistory;
using ContentEntity = RevEx.Domain.Entities.Content;

namespace RevEx.Api.Controllers;

[ApiController]
[Route("api/content/{contentId:int}/history")]
public sealed class ContentHistoryController(RevEx.Application.Abstractions.Identity.ICurrentUser currentUser,

    IRepository<ContentEntity> contentRepository,
    IContentHistoryStore historyStore) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyCollection<ContentHistoryEventDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<ContentHistoryEventDto>>> Get(
        int contentId,
        CancellationToken cancellationToken)
    {
        var events = await historyStore.GetByContentIdAsync(contentId, cancellationToken);
        return Ok(events);
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
        if (string.IsNullOrWhiteSpace(request.Description))
        {
            return ValidationProblem("Description must not be empty.");
        }

        if (await contentRepository.GetByIdAsync(contentId, cancellationToken) is null)
        {
            return NotFound();
        }

        var description = request.Description.Trim();
        if (description.Length > 2000)
        {
            return ValidationProblem("Description must not exceed 2000 characters.");
        }

        var created = await historyStore.AddAsync(
            contentId,
            description,
            currentUser.Author,
            cancellationToken);
        return CreatedAtAction(nameof(Get), new { contentId }, created);
    }

}
