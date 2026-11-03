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
    public Task<Content> AddAsync(Content entity, CancellationToken token) { entity.Id = Items.Count + 1; Items.Add(entity); return Task.FromResult(entity); }
    public Task UpdateAsync(Content entity, CancellationToken token) => Task.CompletedTask;
    public Task DeleteAsync(Content entity, CancellationToken token) { Items.Remove(entity); return Task.CompletedTask; }
}
