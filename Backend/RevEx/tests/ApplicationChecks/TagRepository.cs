using RevEx.Application.Content;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Application.Abstractions.Storage;
using RevEx.Application.Abstractions.Identity;
using RevEx.Domain.Contracts.Content;
using RevEx.Domain.Contracts.ContentHistory;
using RevEx.Domain.Entities;

sealed class TagRepository : ITagRepository
{
    public List<Tag> Items { get; } = [];
    public Task<IReadOnlyCollection<Tag>> GetAllAsync(CancellationToken token) => Task.FromResult<IReadOnlyCollection<Tag>>(Items);
    public Task<Tag?> GetByIdAsync(int id, CancellationToken token) => Task.FromResult(Items.FirstOrDefault(x => x.Id == id));
    public Task<IReadOnlyCollection<Tag>> GetByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken token) =>
        Task.FromResult<IReadOnlyCollection<Tag>>(Items.Where(item => ids.Contains(item.Id)).ToArray());
    public Task<bool> AnyInGroupAsync(int tagGroupId, CancellationToken token) =>
        Task.FromResult(Items.Any(item => item.TagGroupId == tagGroupId));
    public Task<bool> NameExistsAsync(string name, int? excludedId, CancellationToken token) =>
        Task.FromResult(Items.Any(item => item.Id != excludedId && string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)));
    public Task<Tag> AddAsync(Tag entity, CancellationToken token) { entity.Id = Items.Count + 1; Items.Add(entity); return Task.FromResult(entity); }
    public Task UpdateAsync(Tag entity, CancellationToken token) => Task.CompletedTask;
    public Task DeleteAsync(Tag entity, CancellationToken token) { Items.Remove(entity); return Task.CompletedTask; }
}
