using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RevEx.Api.Authorization;
using RevEx.Application.Content;
using RevEx.Domain.Contracts.Content;

namespace RevEx.Api.Controllers;

[ApiController]
[Route("api/content/{contentId:int}/parameters")]
public sealed class ContentParametersController(ContentParameterService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ContentParameterDto>>> List(int contentId, CancellationToken token)
    {
        return Ok(await service.ListAsync(contentId, token));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ContentParameterDto>> Get(int contentId, int id, CancellationToken token)
    {
        return Ok(await service.GetAsync(contentId, id, token));
    }

    [HttpPost]
    [Authorize(Policy = RevExPolicies.EditContent)]
    public async Task<ActionResult<ContentParameterDto>> Create(int contentId, ContentParameterInput input, CancellationToken token)
    {
        var result = await service.CreateAsync(contentId, input, token);
        return CreatedAtAction(nameof(Get), new { contentId, id = result.Id }, result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = RevExPolicies.EditContent)]
    public async Task<IActionResult> Update(int contentId, int id, ContentParameterInput input, CancellationToken token)
    {
        await service.UpdateAsync(contentId, id, input, token);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = RevExPolicies.EditContent)]
    public async Task<IActionResult> Delete(int contentId, int id, CancellationToken token)
    {
        await service.DeleteAsync(contentId, id, token);
        return NoContent();
    }
}
