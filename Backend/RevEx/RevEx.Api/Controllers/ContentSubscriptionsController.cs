using System.Net.Mail;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using RevEx.Api.Subscriptions;
using RevEx.Application.Abstractions.Persistence;
using ContentEntity = RevEx.Domain.Entities.Content;

namespace RevEx.Api.Controllers;

[ApiController]
[Route("api/content/{contentId:int}/subscription")]
public sealed class ContentSubscriptionsController(IContentSubscriptions subscriptions,
    IRepository<ContentEntity> contents) : ControllerBase
{
    private string? UserId => User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

    [HttpGet]
    public async Task<IActionResult> Get(int contentId, CancellationToken token)
    {
        if (UserId is not { } userId) return Unauthorized();
        if (await contents.GetByIdAsync(contentId, token) is null) return NotFound();
        return Ok(await subscriptions.GetAsync(contentId, userId, token));
    }

    [HttpPut]
    public async Task<IActionResult> Subscribe(int contentId, CancellationToken token)
    {
        if (UserId is not { } userId) return Unauthorized();
        if (await contents.GetByIdAsync(contentId, token) is null) return NotFound();
        var email = User.FindFirstValue("email") ?? User.FindFirstValue(ClaimTypes.Email);
        if (!MailAddress.TryCreate(email, out _)) return ValidationProblem("В профиле пользователя отсутствует адрес электронной почты.");
        await subscriptions.SetAsync(contentId, userId, email, true, token);
        return NoContent();
    }

    [HttpDelete]
    public async Task<IActionResult> Unsubscribe(int contentId, CancellationToken token)
    {
        if (UserId is not { } userId) return Unauthorized();
        await subscriptions.SetAsync(contentId, userId, null, false, token);
        return NoContent();
    }
}
