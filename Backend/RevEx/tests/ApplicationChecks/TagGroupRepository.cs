using RevEx.Application.Content;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Application.Abstractions.Storage;
using RevEx.Application.Abstractions.Identity;
using RevEx.Domain.Contracts.Content;
using RevEx.Domain.Contracts.ContentHistory;
using RevEx.Domain.Entities;

sealed class TagGroupRepository : ITagGroupRepository
{
    public List<TagGroup> Items { get; } = [];
    public Task<IReadOnlyCollection<TagGroup>> GetAllAsync(CancellationToken token) => Task.FromResult<IReadOnlyCollection<TagGroup>>(Items);
    public Task<TagGroup?> GetByIdAsync(int id, CancellationToken token) => Task.FromResult(Items.FirstOrDefault(x => x.Id == id));
    public Task<TagGroup> AddAsync(TagGroup entity, CancellationToken token) { entity.Id = Items.Count + 1; Items.Add(entity); return Task.FromResult(entity); }
    public Task UpdateAsync(TagGroup entity, CancellationToken token) => Task.CompletedTask;
    public Task DeleteAsync(TagGroup entity, CancellationToken token) { Items.Remove(entity); return Task.CompletedTask; }
}
