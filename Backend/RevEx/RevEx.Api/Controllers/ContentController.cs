using RevEx.Application.Content;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using RevEx.Api.Authorization;
using RevEx.Api.Models;
using RevEx.Domain.Contracts.Content;

namespace RevEx.Api.Controllers;

[ApiController]
[Route("api/content")]
public sealed class ContentController(ContentService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyCollection<ContentDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<ContentDto>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await service.GetAll(cancellationToken));
    }

    [HttpGet("query")]
    [ProducesResponseType<IReadOnlyCollection<ContentDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<ContentDto>>> Query(
        [FromQuery] ContentQueryDto query,
        CancellationToken cancellationToken)
    {
        return Ok(await service.Query(query, cancellationToken));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<ContentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContentDto>> GetById(int id, CancellationToken cancellationToken)
    {
        return Ok(await service.GetById(id, cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = RevExPolicies.EditContent)]
    [ProducesResponseType<ContentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContentDto>> Create(
        CreateContentDto request,
        CancellationToken cancellationToken)
    {
        var result = await service.Create(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = RevExPolicies.EditContent)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        int id,
        UpdateContentDto request,
        CancellationToken cancellationToken)
    {
        await service.Update(id, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = RevExPolicies.EditContent)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await service.Delete(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/preview")]
    [Authorize(Policy = RevExPolicies.EditContent)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<ContentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContentDto>> UploadPreview(
        int id,
        [FromForm] UploadContentPreviewRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await service.UploadPreview(id, new PreviewUpload(request.File.FileName, request.File.ContentType, request.File.Length, request.File.OpenReadStream), cancellationToken));
    }

    [HttpDelete("{id:int}/preview")]
    [Authorize(Policy = RevExPolicies.EditContent)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePreview(int id, CancellationToken cancellationToken)
    {
        await service.DeletePreview(id, cancellationToken);
        return NoContent();
    }

}
