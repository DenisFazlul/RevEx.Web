using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RevEx.Application.Comments;
using RevEx.Domain.Contracts.Comments;
using RevEx.Domain.Entities;
using RevEx.Infrastructure.Persistence;

namespace RevEx.Infrastructure.Repositories;

public sealed class PostgresCommentStore(IOptions<DatabaseOptions> options) : ICommentStore
{
    private readonly PostgresContextFactory factory = new(options);

    public async Task<IReadOnlyList<CommentSetDto>> GetSetsAsync(
        int offset,
        int limit,
        CancellationToken token)
    {
        await using var database = factory.Create();
        var sets = await database.CommentSets
            .AsNoTracking()
            .OrderByDescending(item => item.CreatedAt)
            .ThenBy(item => item.Id)
            .Skip(offset)
            .Take(limit)
            .Select(item => new CommentSetDto(
                item.Id,
                item.Name,
                item.AuthorId,
                item.Author,
                item.CreatedAt))
            .ToListAsync(token);
        return sets;
    }

    public async Task<CommentSetDto?> GetSetAsync(Guid id, CancellationToken token)
    {
        await using var database = factory.Create();
        var set = await database.CommentSets
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new CommentSetDto(
                item.Id,
                item.Name,
                item.AuthorId,
                item.Author,
                item.CreatedAt))
            .SingleOrDefaultAsync(token);
        return set;
    }

    public async Task<CommentSetDto> CreateSetAsync(
        string name,
        string authorId,
        string author,
        CancellationToken token)
    {
        await using var database = factory.Create();
        var item = new CommentSet
        {
            Id = Guid.NewGuid(),
            Name = name,
            AuthorId = authorId,
            Author = author,
            CreatedAt = UtcNow()
        };
        database.CommentSets.Add(item);
        await database.SaveChangesAsync(token);

        var result = new CommentSetDto(item.Id, item.Name, item.AuthorId, item.Author, item.CreatedAt);
        return result;
    }

    public async Task<IReadOnlyList<CommentDto>?> GetCommentsAsync(
        Guid setId,
        int offset,
        int limit,
        CancellationToken token)
    {
        await using var database = factory.Create();
        var setExists = await database.CommentSets.AnyAsync(item => item.Id == setId, token);
        if (!setExists)
        {
            return null;
        }

        var comments = await database.Comments
            .AsNoTracking()
            .Where(item => item.CommentSetId == setId)
            .OrderBy(item => item.CreatedAt)
            .ThenBy(item => item.Id)
            .Skip(offset)
            .Take(limit)
            .Select(item => new CommentDto(
                item.Id,
                item.CommentSetId,
                item.Text,
                item.AuthorId,
                item.Author,
                item.CreatedAt,
                item.UpdatedAt))
            .ToListAsync(token);
        return comments;
    }

    public async Task<CommentDto?> AddAsync(
        Guid setId,
        string text,
        string authorId,
        string author,
        CancellationToken token)
    {
        await using var database = factory.Create();
        var setExists = await database.CommentSets.AnyAsync(item => item.Id == setId, token);
        if (!setExists)
        {
            return null;
        }

        var item = new Comment
        {
            Id = Guid.NewGuid(),
            CommentSetId = setId,
            Text = text,
            AuthorId = authorId,
            Author = author,
            CreatedAt = UtcNow()
        };
        database.Comments.Add(item);
        await database.SaveChangesAsync(token);

        var result = ToDto(item);
        return result;
    }

    public async Task<CommentDto?> UpdateAsync(
        Guid setId,
        Guid id,
        string text,
        string authorId,
        CancellationToken token)
    {
        await using var database = factory.Create();
        var item = await database.Comments.SingleOrDefaultAsync(
            candidate => candidate.Id == id &&
                candidate.CommentSetId == setId &&
                candidate.AuthorId == authorId,
            token);
        if (item is null)
        {
            return null;
        }

        item.Text = text;
        item.UpdatedAt = UtcNow();
        await database.SaveChangesAsync(token);

        var result = ToDto(item);
        return result;
    }

    public async Task<bool> DeleteAsync(
        Guid setId,
        Guid id,
        string authorId,
        CancellationToken token)
    {
        await using var database = factory.Create();
        var deletedCount = await database.Comments
            .Where(item => item.Id == id &&
                item.CommentSetId == setId &&
                item.AuthorId == authorId)
            .ExecuteDeleteAsync(token);
        return deletedCount > 0;
    }

    private static DateTimeOffset UtcNow()
    {
        var now = DateTimeOffset.UtcNow;
        var result = new DateTimeOffset(now.Ticks - now.Ticks % 10, TimeSpan.Zero);
        return result;
    }

    private static CommentDto ToDto(Comment item)
    {
        var result = new CommentDto(
            item.Id,
            item.CommentSetId,
            item.Text,
            item.AuthorId,
            item.Author,
            item.CreatedAt,
            item.UpdatedAt);
        return result;
    }
}
