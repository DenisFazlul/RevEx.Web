using RevEx.Application.Content;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Application.Abstractions.Storage;
using RevEx.Application.Abstractions.Identity;
using RevEx.Domain.Contracts.Content;
using RevEx.Domain.Contracts.ContentHistory;
using RevEx.Domain.Entities;

sealed class ContentStatusRepository : IContentStatusRepository
{
    public List<ContentStatus> Items { get; } = [];
    public Task<IReadOnlyCollection<ContentStatus>> GetAllAsync(CancellationToken token) => Task.FromResult<IReadOnlyCollection<ContentStatus>>(Items);
    public Task<ContentStatus?> GetByIdAsync(int id, CancellationToken token) => Task.FromResult(Items.FirstOrDefault(x => x.Id == id));
    public Task<ContentStatus?> GetByNameAsync(string name, CancellationToken token) =>
        Task.FromResult(Items.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)));
    public Task<bool> NameExistsAsync(string name, int? excludedId, CancellationToken token) =>
        Task.FromResult(Items.Any(item => item.Id != excludedId && string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)));
    public Task<ContentStatus> AddAsync(ContentStatus entity, CancellationToken token) { entity.Id = Items.Count + 1; Items.Add(entity); return Task.FromResult(entity); }
    public Task UpdateAsync(ContentStatus entity, CancellationToken token) => Task.CompletedTask;
    public Task DeleteAsync(ContentStatus entity, CancellationToken token) { Items.Remove(entity); return Task.CompletedTask; }
}
