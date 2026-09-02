using System.Collections.Concurrent;
using RevEx.Application.Abstractions.Storage;

namespace RevEx.Infrastructure.Storage;

public sealed class InMemoryFileStorage : IFileStorage
{
    private readonly ConcurrentDictionary<int, byte[]> _files = new();

    public async Task SaveAsync(int fileId, Stream content, CancellationToken cancellationToken)
    {
        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        _files[fileId] = buffer.ToArray();
    }

    public Task<Stream?> OpenReadAsync(int fileId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Stream? result = _files.TryGetValue(fileId, out var bytes)
            ? new MemoryStream(bytes, writable: false)
            : null;
        return Task.FromResult(result);
    }

    public Task DeleteAsync(int fileId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _files.TryRemove(fileId, out _);
        return Task.CompletedTask;
    }
}
