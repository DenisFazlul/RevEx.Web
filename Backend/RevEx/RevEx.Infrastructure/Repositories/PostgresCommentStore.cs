using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RevEx.Application.Comments;
using RevEx.Domain.Contracts.Comments;
using RevEx.Domain.Entities;
using RevEx.Infrastructure.Persistence;
namespace RevEx.Infrastructure.Repositories;
public sealed class PostgresCommentStore(IOptions<ContentHistoryOptions> options) : ICommentStore
{
    private readonly PostgresContextFactory factory = new(options);
    public async Task<IReadOnlyList<CommentSetDto>> GetSetsAsync(int offset, int limit, CancellationToken token)
    {
        await using var db = factory.Create();
        return await db.CommentSets.AsNoTracking().OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip(offset).Take(limit)
            .Select(x => new CommentSetDto(x.Id,x.Name,x.AuthorId,x.Author,x.CreatedAt)).ToListAsync(token);
    }
    public async Task<CommentSetDto?> GetSetAsync(Guid id, CancellationToken token)
    {
        await using var db = factory.Create();
        return await db.CommentSets.AsNoTracking().Where(x => x.Id == id).Select(x => new CommentSetDto(x.Id,x.Name,x.AuthorId,x.Author,x.CreatedAt)).SingleOrDefaultAsync(token);
    }
    public async Task<CommentSetDto> CreateSetAsync(string name, string authorId, string author, CancellationToken token)
    {
        await using var db = factory.Create();
        var item = new CommentSet { Id = Guid.NewGuid(), Name = name, AuthorId = authorId, Author = author, CreatedAt = UtcNow() };
        db.CommentSets.Add(item); await db.SaveChangesAsync(token);
        return new(item.Id,item.Name,item.AuthorId,item.Author,item.CreatedAt);
    }
    public async Task<IReadOnlyList<CommentDto>?> GetCommentsAsync(Guid setId, int offset, int limit, CancellationToken token)
    {
        await using var db = factory.Create();
        if (!await db.CommentSets.AnyAsync(x => x.Id == setId, token)) return null;
        return await db.Comments.AsNoTracking().Where(x => x.CommentSetId == setId).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Skip(offset).Take(limit)
            .Select(x => new CommentDto(x.Id,x.CommentSetId,x.Text,x.AuthorId,x.Author,x.CreatedAt,x.UpdatedAt)).ToListAsync(token);
    }
    public async Task<CommentDto?> AddAsync(Guid setId, string text, string authorId, string author, CancellationToken token)
    {
        await using var db = factory.Create();
        if (!await db.CommentSets.AnyAsync(x => x.Id == setId, token)) return null;
        var item = new Comment { Id = Guid.NewGuid(), CommentSetId = setId, Text = text, AuthorId = authorId, Author = author, CreatedAt = UtcNow() };
        db.Comments.Add(item); await db.SaveChangesAsync(token); return ToDto(item);
    }
    public async Task<CommentDto?> UpdateAsync(Guid setId, Guid id, string text, string authorId, CancellationToken token)
    {
        await using var db = factory.Create();
        var item = await db.Comments.SingleOrDefaultAsync(x => x.Id == id && x.CommentSetId == setId && x.AuthorId == authorId, token);
        if (item == null) return null;
        item.Text = text; item.UpdatedAt = UtcNow(); await db.SaveChangesAsync(token); return ToDto(item);
    }
    public async Task<bool> DeleteAsync(Guid setId, Guid id, string authorId, CancellationToken token)
    {
        await using var db = factory.Create();
        return await db.Comments.Where(x => x.Id == id && x.CommentSetId == setId && x.AuthorId == authorId).ExecuteDeleteAsync(token) > 0;
    }
    private static DateTimeOffset UtcNow() { var now = DateTimeOffset.UtcNow; return new DateTimeOffset(now.Ticks - now.Ticks % 10, TimeSpan.Zero); }
    private static CommentDto ToDto(Comment x) => new(x.Id,x.CommentSetId,x.Text,x.AuthorId,x.Author,x.CreatedAt,x.UpdatedAt);
}
