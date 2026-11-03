using System.Net.Mail;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using RevEx.Application.Subscriptions;
using ContentEntity = RevEx.Domain.Entities.Content;

namespace RevEx.Api.Controllers;

[ApiController]
[Route("api/content/{contentId:int}/subscription")]
public sealed class ContentSubscriptionsController(ContentSubscriptionService service) : ControllerBase
{
    private string? UserId => User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

    [HttpGet]
    public async Task<IActionResult> Get(int contentId, CancellationToken token)
    {
        if (UserId is not { } userId) return Unauthorized();
        return Ok(await service.Get(contentId, userId, token));
    }

    [HttpPut]
    public async Task<IActionResult> Subscribe(int contentId, CancellationToken token)
    {
        if (UserId is not { } userId) return Unauthorized();
        var email = User.FindFirstValue("email") ?? User.FindFirstValue(ClaimTypes.Email);
        await service.Subscribe(contentId, userId, email, token);
        return NoContent();
    }

    [HttpDelete]
    public async Task<IActionResult> Unsubscribe(int contentId, CancellationToken token)
    {
        if (UserId is not { } userId) return Unauthorized();
        await service.Unsubscribe(contentId, userId, token);
        return NoContent();
    }
}
