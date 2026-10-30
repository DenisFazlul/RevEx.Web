using RevEx.Application.Content;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Application.Abstractions.Storage;
using RevEx.Application.Abstractions.Identity;
using RevEx.Domain.Contracts.Content;
using RevEx.Domain.Contracts.ContentHistory;
using RevEx.Domain.Entities;

sealed class Storage : IFileStorage
{
    public bool Fail;
    public List<int> Deleted = [];
    public Task SaveAsync(int id, Stream content, CancellationToken token) => Fail ? Task.FromException(new IOException("Storage unavailable")) : Task.CompletedTask;
    public Task<Stream?> OpenReadAsync(int id, CancellationToken token) => Task.FromResult<Stream?>(null);
    public Task DeleteAsync(int id, CancellationToken token) { Deleted.Add(id); return Task.CompletedTask; }
}
