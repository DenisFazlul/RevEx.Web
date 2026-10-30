using RevEx.Application.Content;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Application.Abstractions.Storage;
using RevEx.Application.Abstractions.Identity;
using RevEx.Domain.Contracts.Content;
using RevEx.Domain.Contracts.ContentHistory;
using RevEx.Domain.Entities;

sealed class Repository<T> : IRepository<T> where T : class
{
    public List<T> Items { get; } = [];
    public Task<IReadOnlyCollection<T>> GetAllAsync(CancellationToken token) => Task.FromResult<IReadOnlyCollection<T>>(Items);
    public Task<T?> GetByIdAsync(int id, CancellationToken token) => Task.FromResult(Items.FirstOrDefault(x => (int)typeof(T).GetProperty("Id")!.GetValue(x)! == id));
    public Task<T> AddAsync(T entity, CancellationToken token) { typeof(T).GetProperty("Id")!.SetValue(entity, Items.Count + 1); Items.Add(entity); return Task.FromResult(entity); }
    public Task UpdateAsync(T entity, CancellationToken token) => Task.CompletedTask;
    public Task DeleteAsync(T entity, CancellationToken token) { Items.Remove(entity); return Task.CompletedTask; }
}
