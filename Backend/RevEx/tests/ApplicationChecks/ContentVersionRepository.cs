using RevEx.Application.Content;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Application.Abstractions.Storage;
using RevEx.Application.Abstractions.Identity;
using RevEx.Domain.Contracts.Content;
using RevEx.Domain.Contracts.ContentHistory;
using RevEx.Domain.Entities;

sealed class ContentVersionRepository : IContentVersionRepository
{
    public List<ContentVersion> Items { get; } = [];
    public Task<IReadOnlyCollection<ContentVersion>> GetAllAsync(CancellationToken token) => Task.FromResult<IReadOnlyCollection<ContentVersion>>(Items);
    public Task<ContentVersion?> GetByIdAsync(int id, CancellationToken token) => Task.FromResult(Items.FirstOrDefault(x => x.Id == id));
    public Task<ContentVersion> AddAsync(ContentVersion entity, CancellationToken token) { entity.Id = Items.Count + 1; Items.Add(entity); return Task.FromResult(entity); }
    public Task UpdateAsync(ContentVersion entity, CancellationToken token) => Task.CompletedTask;
    public Task DeleteAsync(ContentVersion entity, CancellationToken token) { Items.Remove(entity); return Task.CompletedTask; }
}
