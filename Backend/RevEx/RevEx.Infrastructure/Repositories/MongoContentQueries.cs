using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Domain.Contracts.Content;
using RevEx.Domain.Entities;
using RevEx.Infrastructure.Persistence;
namespace RevEx.Infrastructure.Repositories;
public sealed class MongoContentQueries(MongoDbContext context) : IContentQueries
{
    public async Task<IReadOnlyCollection<ContentDto>> QueryAsync(ContentQueryDto query, CancellationToken token)
    {
        IQueryable<Content> contents = context.Set<Content>().AsNoTracking();
        if (query.CategoryIds.Length > 0) contents = contents.Where(x => query.CategoryIds.Contains(x.CategoryId));
        foreach (var tagId in query.TagIds) { var id = tagId; contents = contents.Where(x => x.TagIds.Contains(id)); }
        if (!string.IsNullOrWhiteSpace(query.Name))
        {
            var pattern = Regex.Escape(query.Name.Trim());
            contents = contents.Where(x => Regex.IsMatch(x.Name, pattern, RegexOptions.IgnoreCase));
        }
        contents = contents.OrderBy(x => x.Id).Skip(query.Offset);
        if (query.Limit is int limit) contents = contents.Take(limit);
        return (await contents.ToListAsync(token)).Select(x => new ContentDto(x.Id, x.Name, x.Description,
            x.CategoryId, x.ContentStatusId, x.PreviewFileId, x.TagIds.ToArray(), x.CreatedAt, x.UpdatedAt)).ToArray();
    }
}
