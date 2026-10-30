using RevEx.Application.Content;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Application.Abstractions.Storage;
using RevEx.Application.Abstractions.Identity;
using RevEx.Domain.Contracts.Content;
using RevEx.Domain.Contracts.ContentHistory;
using RevEx.Domain.Entities;

sealed class Queries : IContentQueries
{
    public ContentQueryDto? Last;
    public Task<IReadOnlyCollection<ContentDto>> QueryAsync(ContentQueryDto query, CancellationToken token) { Last = query; return Task.FromResult<IReadOnlyCollection<ContentDto>>([]); }
}
