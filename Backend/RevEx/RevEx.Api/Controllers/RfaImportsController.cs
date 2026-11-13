using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RevEx.Api.Authorization;
using RevEx.Api.Models;
using RevEx.Application.Catalog;
using RevEx.Domain.Contracts.ContentVersion;

namespace RevEx.Api.Controllers;

[ApiController]
[Route("api/imports/rfa")]
public sealed class RfaImportsController(RfaImportsService service) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(550 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 540 * 1024 * 1024, ValueLengthLimit = 8 * 1024 * 1024)]
    [Authorize(Policy = RevExPolicies.EditContent)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<ContentVersionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ContentVersionDto>> Import(
        [FromForm] RfaImportForm form,
        CancellationToken cancellationToken)
    {
        var primaryFile = new VersionFileUpload(
            form.File.FileName,
            form.File.Length,
            form.File.OpenReadStream,
            "primary",
            form.MetadataJson);
        var auxiliaryFiles = form.AuxiliaryFiles
            .Select(file => new RfaAuxiliaryFileUpload(file.FileName, file.Length, file.OpenReadStream))
            .ToArray();
        var request = new RfaImportRequest(
            form.Cid,
            form.Vid,
            form.ContentName,
            form.Description,
            form.CategoryId,
            form.VersionName,
            form.Date,
            form.ApplicationVersion,
            primaryFile,
            auxiliaryFiles);

        var result = await service.Import(request, cancellationToken);
        return Ok(result);
    }
}
