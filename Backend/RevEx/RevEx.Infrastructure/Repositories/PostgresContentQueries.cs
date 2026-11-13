using Microsoft.EntityFrameworkCore;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Domain.Contracts.Content;
using RevEx.Domain.Entities;
using RevEx.Infrastructure.Persistence;

namespace RevEx.Infrastructure.Repositories;

public sealed class PostgresContentQueries(PostgresDbContext context) : IContentQueries
{
    public async Task<IReadOnlyCollection<ContentDto>> QueryAsync(
        ContentQueryDto query,
        CancellationToken token)
    {
        IQueryable<Content> contents = context.Contents.AsNoTracking();
        if (query.Cid is Guid cid)
        {
            contents = contents.Where(entity => entity.Cid == cid);
        }

        if (query.CategoryIds.Length > 0)
        {
            contents = contents.Where(entity => query.CategoryIds.Contains(entity.CategoryId));
        }

        foreach (var tagId in query.TagIds)
        {
            var id = tagId;
            contents = contents.Where(entity => entity.Tags.Any(tag => tag.Id == id));
        }

        if (!string.IsNullOrWhiteSpace(query.Name))
        {
            var pattern = $"%{EscapeLikePattern(query.Name.Trim())}%";
            contents = contents.Where(entity => EF.Functions.ILike(entity.Name, pattern, "\\"));
        }

        contents = contents.OrderBy(entity => entity.Id).Skip(query.Offset);
        if (query.Limit is int limit)
        {
            contents = contents.Take(limit);
        }

        var entities = await contents
            .Include(entity => entity.Tags)
            .ToListAsync(token);
        var result = entities
            .Select(entity => new ContentDto(
                entity.Id,
                entity.Cid,
                entity.Name,
                entity.Description,
                entity.CategoryId,
                entity.ContentStatusId,
                entity.PreviewFileId,
                entity.Tags.Select(tag => tag.Id).ToArray(),
                entity.CreatedAt,
                entity.UpdatedAt))
            .ToArray();
        return result;
    }

    private static string EscapeLikePattern(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
    }
}
