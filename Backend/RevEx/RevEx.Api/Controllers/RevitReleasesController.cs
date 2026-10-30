using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RevEx.Api.Authorization;
using RevEx.Application.Abstractions.Storage;

namespace RevEx.Api.Controllers;

[ApiController]
[Route("api/revit-releases")]
[Authorize(Policy = RevExPolicies.AdministerCatalog)]
public sealed class RevitReleasesController(IRevitReleaseStore releases) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await releases.ListAsync(ct));

    [AllowAnonymous]
    [HttpGet("installers")]
    public async Task<IActionResult> Installers(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return Ok((await releases.ListAsync(ct)).Where(x => x.IsPublished)
            .GroupBy(x => x.RevitVersion).Select(g => g.OrderByDescending(x => Version.Parse(x.Version)).First()));
    }

    [HttpPost]
    [RequestSizeLimit(1073741824)]
    [RequestFormLimits(MultipartBodyLengthLimit = 1073741824)]
    public async Task<IActionResult> Upload([FromForm] RevitUpload request, CancellationToken ct)
    {
        if (request.File == null || request.File.Length == 0) return BadRequest(new { message = "Выберите установщик." });
        try
        {
            await using var stream = request.File.OpenReadStream();
            return Ok(await releases.UploadAsync(request.Version, request.RevitVersion, request.Notes, request.File.FileName, stream, ct));
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpPost("{id}/publish")]
    public async Task<IActionResult> Publish(string id, [FromBody] RevitPublication request, CancellationToken ct)
        => await releases.PublishAsync(id, request.IsPublished, ct) ? NoContent() : NotFound();

    [AllowAnonymous]
    [HttpGet("{id}/download")]
    public async Task<IActionResult> Download(string id, CancellationToken ct)
    {
        var file = await releases.GetFileAsync(id, ct);
        return file == null ? NotFound() : PhysicalFile(file.Value.Path, "application/octet-stream", file.Value.Name, enableRangeProcessing: true);
    }
}
