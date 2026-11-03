using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RevEx.Application.Abstractions.Identity;
using RevEx.Application.Comments;
using RevEx.Domain.Contracts.Comments;
namespace RevEx.Api.Controllers;
[ApiController]
[Authorize]
[Route("api/comment-sets")]
public sealed class CommentSetsController(ICommentStore store, ICurrentUser currentUser) : ControllerBase
{
    private string? AuthorId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CommentSetDto>>> GetSets([FromQuery] int offset = 0, [FromQuery] int limit = 50, CancellationToken token = default)
    {
        if (offset < 0 || limit < 1 || limit > 200) return BadRequest("Offset must be nonnegative; limit must be 1–200.");
        return Ok(await store.GetSetsAsync(offset,limit,token));
    }
    [HttpGet("{setId:guid}")]
    public async Task<ActionResult<CommentSetDto>> GetSet(Guid setId, CancellationToken token)
    {
        var item = await store.GetSetAsync(setId,token); return item == null ? NotFound() : Ok(item);
    }
    [HttpPost]
    public async Task<ActionResult<CommentSetDto>> CreateSet(CreateCommentSetDto request, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest("Name must not be empty.");
        if (AuthorId is not {} authorId) return Forbid();
        var item = await store.CreateSetAsync(request.Name.Trim(),authorId,currentUser.Author,token);
        return CreatedAtAction(nameof(GetSet),new { setId = item.Id },item);
    }
    [HttpGet("{setId:guid}/comments")]
    public async Task<ActionResult<IReadOnlyList<CommentDto>>> GetComments(Guid setId, [FromQuery] int offset = 0, [FromQuery] int limit = 50, CancellationToken token = default)
    {
        if (offset < 0 || limit < 1 || limit > 200) return BadRequest("Offset must be nonnegative; limit must be 1–200.");
        var items = await store.GetCommentsAsync(setId,offset,limit,token); return items == null ? NotFound() : Ok(items);
    }
    [HttpPost("{setId:guid}/comments")]
    public async Task<ActionResult<CommentDto>> Add(Guid setId, SaveCommentDto request, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(request.Text)) return BadRequest("Text must not be empty.");
        if (AuthorId is not {} authorId) return Forbid();
        var item = await store.AddAsync(setId,request.Text.Trim(),authorId,currentUser.Author,token);
        return item == null ? NotFound() : CreatedAtAction(nameof(GetComments),new { setId },item);
    }
    [HttpPut("{setId:guid}/comments/{id:guid}")]
    public async Task<ActionResult<CommentDto>> Update(Guid setId, Guid id, SaveCommentDto request, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(request.Text)) return BadRequest("Text must not be empty.");
        if (AuthorId is not {} authorId) return Forbid();
        var item = await store.UpdateAsync(setId,id,request.Text.Trim(),authorId,token); return item == null ? NotFound() : Ok(item);
    }
    [HttpDelete("{setId:guid}/comments/{id:guid}")]
    public async Task<IActionResult> Delete(Guid setId, Guid id, CancellationToken token)
    {
        if (AuthorId is not {} authorId) return Forbid();
        return await store.DeleteAsync(setId,id,authorId,token) ? NoContent() : NotFound();
    }
}
