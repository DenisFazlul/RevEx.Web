using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RevEx.Api.Authorization;
using RevEx.Api.Models;
using RevEx.Application.Catalog;
using RevEx.Domain.Contracts.ContentVersion;

namespace RevEx.Api.Controllers;

[ApiController]
[Route("api/imports/versions")]
public sealed class VersionImportsController(VersionImportsService service) : ControllerBase
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
        [FromForm] VersionImportForm form,
        CancellationToken cancellationToken)
    {
        var primaryFile = new VersionFileUpload(
            form.File.FileName,
            form.File.Length,
            form.File.OpenReadStream,
            "primary",
            form.MetadataJson);
        var auxiliaryFiles = form.AuxiliaryFiles
            .Select((file, index) => new VersionImportAuxiliaryUpload(
                file.FileName, file.Length, file.OpenReadStream, form.AuxiliaryMetadataJson.GetValueOrDefault(index)))
            .ToArray();
        var request = new VersionImportRequest(
            form.Cid,
            form.Vid,
            form.ContentName,
            form.Description,
            form.CategoryId,
            form.VersionName,
            form.Date,
            form.ApplicationVersion,
            primaryFile,
            auxiliaryFiles,
            form.Platform!.Value,
            form.Application);

        var result = await service.Import(request, cancellationToken);
        return Ok(result);
    }
}
