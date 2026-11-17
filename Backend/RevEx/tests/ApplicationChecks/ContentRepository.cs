using RevEx.Application.Content;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Application.Abstractions.Storage;
using RevEx.Application.Abstractions.Identity;
using RevEx.Domain.Contracts.Content;
using RevEx.Domain.Contracts.ContentHistory;
using RevEx.Domain.Entities;

sealed class ContentRepository : IContentRepository
{
    public List<Content> Items { get; } = [];
    public Task<IReadOnlyCollection<Content>> GetAllAsync(CancellationToken token) => Task.FromResult<IReadOnlyCollection<Content>>(Items);
    public Task<Content?> GetByIdAsync(int id, CancellationToken token) => Task.FromResult(Items.FirstOrDefault(x => x.Id == id));
    public Task<bool> SetFrozenAsync(int id, bool isFrozen, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var item = Items.FirstOrDefault(content => content.Id == id);
        if (item is null)
        {
            return Task.FromResult(false);
        }

        item.IsFrozen = isFrozen;
        return Task.FromResult(true);
    }

    public Task<Content?> GetByCidAsync(Guid cid, CancellationToken token) => Task.FromResult(Items.FirstOrDefault(x => x.Cid == cid));
    public Task<bool> AnyWithCategoryIdAsync(int categoryId, CancellationToken token) =>
        Task.FromResult(Items.Any(item => item.CategoryId == categoryId));
    public Task<bool> AnyWithStatusIdAsync(int statusId, CancellationToken token) =>
        Task.FromResult(Items.Any(item => item.ContentStatusId == statusId));
    public Task RemoveTagFromAllAsync(int tagId, CancellationToken token)
    {
        foreach (var item in Items)
        {
            item.TagIds = item.TagIds.Where(id => id != tagId).ToArray();
            item.Tags = item.Tags.Where(tag => tag.Id != tagId).ToArray();
        }

        return Task.CompletedTask;
    }
    public Task<Content> AddAsync(Content entity, CancellationToken token) { entity.Id = Items.Count + 1; Items.Add(entity); return Task.FromResult(entity); }
    public Task UpdateAsync(Content entity, CancellationToken token) => Task.CompletedTask;
    public Task DeleteAsync(Content entity, CancellationToken token) { Items.Remove(entity); return Task.CompletedTask; }
}
