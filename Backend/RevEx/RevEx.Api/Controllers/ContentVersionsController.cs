using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using RevEx.Api.Authorization;
using RevEx.Api.Models;
using RevEx.Application.Abstractions.Storage;
using RevEx.Domain.Contracts.ContentVersion;
using RevEx.Domain.Entities;
using ContentEntity = RevEx.Domain.Entities.Content;

namespace RevEx.Api.Controllers;

[ApiController]
[Route("api/content-versions")]
public sealed class ContentVersionsController(RevEx.Application.Catalog.ContentVersionsService service) : ControllerBase
{

    [HttpGet]
    [ProducesResponseType<IReadOnlyCollection<ContentVersionDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<ContentVersionDto>>> GetAll(
        CancellationToken cancellationToken)
    {
        return Ok(await service.GetAll(cancellationToken));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<ContentVersionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContentVersionDto>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        return Ok(await service.GetById(id, cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = RevExPolicies.EditContent)]
    [ProducesResponseType<ContentVersionDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContentVersionDto>> Create(
        CreateContentVersionDto request,
        CancellationToken cancellationToken)
    {
        var result = await service.Create(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPost("{id:int}/file")]
    [RequestSizeLimit(110 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 100 * 1024 * 1024, ValueLengthLimit = 8 * 1024 * 1024)]
    [Authorize(Policy = RevExPolicies.EditContent)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<ContentVersionDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContentVersionDto>> AddFile(
        int id,
        [FromForm] UploadFileRequest request,
        CancellationToken cancellationToken)
    {
        var upload = new RevEx.Application.Catalog.VersionFileUpload(
            request.File.FileName,
            request.File.Length,
            request.File.OpenReadStream,
            request.Role,
            request.MetadataJson);
        var result = await service.AddFile(id, upload, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpDelete("{id:int}/files/{fileId:int}")]
    [Authorize(Policy = RevExPolicies.EditContent)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteFile(
        int id,
        int fileId,
        CancellationToken cancellationToken)
    {
        await service.DeleteFile(id, fileId, cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = RevExPolicies.EditContent)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        int id,
        UpdateContentVersionDto request,
        CancellationToken cancellationToken)
    {
        await service.Update(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:int}/publish-status")]
    [Authorize(Policy = RevExPolicies.EditContent)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePublishStatus(
        int id, UpdatePublishStatusDto request, CancellationToken cancellationToken)
    {
        await service.UpdatePublishStatus(id, request, cancellationToken);
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
}
