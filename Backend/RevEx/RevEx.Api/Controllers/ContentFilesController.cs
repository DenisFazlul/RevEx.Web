using Microsoft.AspNetCore.Mvc;
using RevEx.Application.Abstractions.Storage;
using RevEx.Domain.Contracts.ContentFile;
using RevEx.Domain.Entities;

namespace RevEx.Api.Controllers;

[ApiController]
[Route("api/content-files")]
public sealed class ContentFilesController(RevEx.Application.Catalog.ContentFilesService service) : ControllerBase
{
 [HttpGet]
 public async Task<ActionResult<IReadOnlyCollection<ContentFileDto>>> GetAll(CancellationToken token) => Ok(await service.GetAll(token));
 [HttpGet("{id:int}")]
 public async Task<ActionResult<ContentFileDto>> GetById(int id, CancellationToken token) => Ok(await service.GetById(id, token));
 [HttpGet("{id:int}/content")]
 [Produces("application/octet-stream")]
 public async Task<IActionResult> GetContent(int id, CancellationToken token)
 {
  var file = await service.Download(id, token);
  return File(file.Stream, "application/octet-stream", file.FileName);
 }
}
