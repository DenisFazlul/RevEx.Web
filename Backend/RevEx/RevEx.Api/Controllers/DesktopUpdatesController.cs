using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RevEx.Api.Authorization;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Application.Abstractions.Storage;
using RevEx.Domain.Contracts.DesktopReleases;

namespace RevEx.Api.Controllers;

[ApiController]
[Route("api/desktop-updates")]
public sealed class DesktopUpdatesController(RevEx.Application.Abstractions.Identity.ICurrentUser currentUser,
    IDesktopReleaseStore releases, IDesktopUpdateTrackingStore tracking) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("check")]
    [ProducesResponseType<DesktopUpdateResponse>(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> Check(DesktopInstallationRequest request, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        try
        {
            var policy = await releases.CheckAsync(request.Version, request.Platform, cancellationToken);
            if (policy == null) return StatusCode(503, new { message = "Администратор ещё не опубликовал активный релиз десктопа." });
            await tracking.ObserveAsync(request, null, null, cancellationToken);
            var offer = policy.Package == null ? null : await tracking.CreateOfferAsync(request, policy, cancellationToken);
            return Ok(new DesktopUpdateResponse(policy.Decision, policy.TargetVersion, policy.Notes, policy.Package,
                policy.IsDowngrade, offer?.Id, offer == null ? null : $"api/desktop-updates/offers/{offer.Id}/package"));
        }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
        catch (UnauthorizedAccessException) { return Unauthorized(); }
    }

    [Authorize]
    [HttpPost("report")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> Report(DesktopInstallationRequest request, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Unauthorized();
        try
        {
            await tracking.ObserveAsync(request, userId, currentUser.Author, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
        catch (UnauthorizedAccessException) { return Unauthorized(); }
    }

    [Authorize(Policy = RevExPolicies.AdministerCatalog)]
    [HttpGet("history")]
    [ProducesResponseType<DesktopUpdateHistory>(200)]
    public async Task<IActionResult> History([FromQuery] int limit = 200, CancellationToken cancellationToken = default)
        => Ok(await tracking.GetHistoryAsync(limit, cancellationToken));

    [AllowAnonymous]
    [HttpPost("offers/{id:guid}/failure")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Failure(Guid id, DesktopUpdateFailure request,
        [FromHeader(Name = "X-RevEx-Installation-Key")] string key, CancellationToken cancellationToken)
    {
        try { await tracking.FailOfferAsync(id, key, request.Message, cancellationToken); return NoContent(); }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
    }

    [AllowAnonymous]
    [HttpGet("offers/{id:guid}/package")]
    [Produces("application/octet-stream")]
    [ProducesResponseType(200)]
    [ProducesResponseType(206)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Download(Guid id, [FromHeader(Name = "X-RevEx-Installation-Key")] string key, CancellationToken cancellationToken)
    {
        var offer = await tracking.GetOfferAsync(id, key, cancellationToken);
        if (offer?.Policy.Package is not { } package) return NotFound();
        // Activation changes cannot retarget a previously issued offer.
        var path = await releases.GetPublishedPackageAsync(package.Version, package.FileName, cancellationToken);
        if (path == null) return NotFound();
        try
        {
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            return new TrackedDesktopFileResult(File(stream, "application/octet-stream", package.FileName, enableRangeProcessing: true),
                offer, package.Version, package.FileName);
        }
        catch (FileNotFoundException) { return NotFound(); }
        catch (DirectoryNotFoundException) { return NotFound(); }
    }
}
