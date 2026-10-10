using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using RevEx.Api.Authorization;
using RevEx.Application.Abstractions.Storage;
using RevEx.Application.DTOs.DesktopReleases;

namespace RevEx.Api.Controllers;

[ApiController]
[Route("api/desktop-releases")]
[Authorize(Policy = RevExPolicies.AdministerCatalog)]
public sealed class DesktopReleasesController(IDesktopReleaseStore releases) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) => Ok(await releases.ListAsync(cancellationToken));

    [HttpPost]
    [RequestSizeLimit(1073741824)]
    [RequestFormLimits(MultipartBodyLengthLimit = 1073741824)]
    public async Task<IActionResult> Upload([FromForm] DesktopReleaseUpload request, CancellationToken cancellationToken)
    {
        if (request.File == null || request.File.Length == 0) return BadRequest(new { message = "Выберите ZIP-архив релиза." });
        try
        {
            await using var stream = request.File.OpenReadStream();
            return Ok(await releases.UploadAsync(stream, cancellationToken));
        }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
    }

    [HttpPost("{version}/activate")]
    public async Task<IActionResult> Activate(string version, [FromBody] DesktopReleaseActivation activation, CancellationToken cancellationToken)
    {
        if (activation.Version != version) return BadRequest(new { message = "Версия в адресе и теле запроса должна совпадать." });
        try { await releases.ActivateAsync(activation, cancellationToken); return NoContent(); }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
    }

    [HttpPatch("{version}")]
    public async Task<IActionResult> Edit(string version, [FromBody] DesktopReleaseEdit edit, CancellationToken cancellationToken)
    {
        try { return Ok(await releases.EditAsync(version, edit, cancellationToken)); }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
    }

    [HttpDelete("{version}")]
    public async Task<IActionResult> Delete(string version, CancellationToken cancellationToken)
    {
        try { await releases.DeleteAsync(version, cancellationToken); return NoContent(); }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
    }

    [HttpPost("{version}/restore")]
    public async Task<IActionResult> Restore(string version, CancellationToken cancellationToken)
    {
        try { await releases.RestoreAsync(version, cancellationToken); return NoContent(); }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
    }

    [AllowAnonymous]
    [HttpGet("/api/desktop-release")]
    public async Task<IActionResult> Active(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var release = await releases.GetActiveAsync(cancellationToken);
        if (release == null) return NotFound(new { message = "Администратор ещё не опубликовал активный релиз десктопа." });
        // Bridge for installed clients using the previous protocol: require the server-selected target.
        // Admin policy has no ranges; new clients use /api/desktop-updates/check.
        return Ok(new { release.BackendVersion, release.RecommendedDesktopVersion,
            MinimumDesktopVersion = release.RecommendedDesktopVersion, MaximumDesktopVersion = release.RecommendedDesktopVersion,
            release.UpdatesPath, release.Installers, release.Notes });
    }

    [AllowAnonymous]
    [HttpGet("{version}/files/{fileName}")]
    public async Task<IActionResult> Download(string version, string fileName, CancellationToken cancellationToken)
    {
        var path = await releases.GetPublicFileAsync(version, fileName, cancellationToken);
        if (path == null) return NotFound();
        try
        {
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            return new TrackedDesktopFileResult(File(stream, "application/octet-stream", fileName, enableRangeProcessing: true),
                null, version, fileName, User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier),
                User.Identity?.IsAuthenticated == true ? ContentHistoryController.ResolveAuthor(User) : null);
        }
        catch (FileNotFoundException) { return NotFound(); }
        catch (DirectoryNotFoundException) { return NotFound(); }
    }
}

public sealed class DesktopReleaseUpload
{
    public IFormFile File { get; set; } = null!;
}
