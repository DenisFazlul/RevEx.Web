using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Domain.Contracts.Content;
using RevEx.Domain.Entities;
using RevEx.Infrastructure.Persistence;
namespace RevEx.Infrastructure.Repositories;

public sealed class MongoContentQueries(MongoDbContext context) : IContentQueries
{
    public async Task<IReadOnlyCollection<ContentDto>> QueryAsync(ContentQueryDto query, CancellationToken cancellationToken)
    {
        var filters = Builders<Content>.Filter;
        var filter = filters.Empty;
        if (query.CategoryIds.Length > 0) filter &= filters.In(x => x.CategoryId, query.CategoryIds);
        if (query.TagIds.Length > 0) filter &= filters.All(x => x.TagIds, query.TagIds);
        if (!string.IsNullOrWhiteSpace(query.Name))
            filter &= filters.Regex(x => x.Name, new BsonRegularExpression(Regex.Escape(query.Name.Trim()), "i"));
        var cursor = context.Collection<Content>().Find(filter).SortBy(x => x.Id).Skip(query.Offset);
        if (query.Limit is int limit) cursor = cursor.Limit(limit);
        var contents = await cursor.ToListAsync(cancellationToken);
        return contents.Select(entity => new ContentDto(entity.Id, entity.Name, entity.Description,
            entity.CategoryId, entity.ContentStatusId, entity.PreviewFileId, entity.TagIds.ToArray(),
            entity.CreatedAt, entity.UpdatedAt)).ToArray();
    }
}
