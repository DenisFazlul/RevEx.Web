using RevEx.Application.Content;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Application.Abstractions.Storage;
using RevEx.Application.Abstractions.Identity;
using RevEx.Domain.Contracts.Content;
using RevEx.Domain.Contracts.ContentHistory;
using RevEx.Domain.Entities;

sealed class ContentFileRepository : IContentFileRepository
{
    public List<ContentFile> Items { get; } = [];
    public Task<IReadOnlyCollection<ContentFile>> GetAllAsync(CancellationToken token) => Task.FromResult<IReadOnlyCollection<ContentFile>>(Items);
    public Task<ContentFile?> GetByIdAsync(int id, CancellationToken token) => Task.FromResult(Items.FirstOrDefault(x => x.Id == id));
    public Task<ContentFile> AddAsync(ContentFile entity, CancellationToken token) { entity.Id = Items.Count + 1; Items.Add(entity); return Task.FromResult(entity); }
    public Task UpdateAsync(ContentFile entity, CancellationToken token) => Task.CompletedTask;
    public Task DeleteAsync(ContentFile entity, CancellationToken token) { Items.Remove(entity); return Task.CompletedTask; }
}
