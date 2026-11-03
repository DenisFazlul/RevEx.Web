using RevEx.Application.Content;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Application.Abstractions.Storage;
using RevEx.Application.Abstractions.Identity;
using RevEx.Domain.Contracts.Content;
using RevEx.Domain.Contracts.ContentHistory;
using RevEx.Domain.Entities;

sealed class CategoryRepository : ICategoryRepository
{
    public List<Category> Items { get; } = [];
    public Task<IReadOnlyCollection<Category>> GetAllAsync(CancellationToken token) => Task.FromResult<IReadOnlyCollection<Category>>(Items);
    public Task<Category?> GetByIdAsync(int id, CancellationToken token) => Task.FromResult(Items.FirstOrDefault(x => x.Id == id));
    public Task<Category> AddAsync(Category entity, CancellationToken token) { entity.Id = Items.Count + 1; Items.Add(entity); return Task.FromResult(entity); }
    public Task UpdateAsync(Category entity, CancellationToken token) => Task.CompletedTask;
    public Task DeleteAsync(Category entity, CancellationToken token) { Items.Remove(entity); return Task.CompletedTask; }
}
