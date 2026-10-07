using Microsoft.AspNetCore.Authorization;
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
    }

    [AllowAnonymous]
    [HttpGet("/api/desktop-release")]
    public async Task<IActionResult> Active(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var release = await releases.GetActiveAsync(cancellationToken);
        return release == null ? NotFound(new { message = "Администратор ещё не активировал релиз десктопа." }) : Ok(release);
    }

    [AllowAnonymous]
    [HttpGet("{version}/files/{fileName}")]
    public async Task<IActionResult> Download(string version, string fileName, CancellationToken cancellationToken)
    {
        var path = await releases.GetPublicFileAsync(version, fileName, cancellationToken);
        return path == null ? NotFound() : PhysicalFile(path, fileName.EndsWith(".json") ? "application/json" : "application/octet-stream", fileName, enableRangeProcessing: true);
    }
}

public sealed class DesktopReleaseUpload
{
    public IFormFile File { get; set; } = null!;
}
