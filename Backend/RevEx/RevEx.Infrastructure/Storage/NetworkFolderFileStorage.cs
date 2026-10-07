using RevEx.Application.Abstractions.Storage;

namespace RevEx.Infrastructure.Storage;

public sealed class NetworkFolderFileStorage : IFileStorage
{
    private readonly string _rootPath;

    public NetworkFolderFileStorage(NetworkFolderStorageOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Path);
        _rootPath = Path.GetFullPath(options.Path);
        Directory.CreateDirectory(_rootPath);
    }

    public async Task SaveAsync(int fileId, Stream content, CancellationToken cancellationToken)
    {
        var destinationPath = GetFilePath(fileId);
        var temporaryPath = Path.Combine(_rootPath, $".{fileId}.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var output = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await content.CopyToAsync(output, cancellationToken);
                await output.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, destinationPath, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    public Task<Stream?> OpenReadAsync(int fileId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = GetFilePath(fileId);
        Stream? stream = File.Exists(path)
            ? new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan)
            : null;
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(int fileId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        File.Delete(GetFilePath(fileId));
        return Task.CompletedTask;
    }

    private string GetFilePath(int fileId)
    {
        if (fileId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fileId), "File id must be positive.");
        }

        return Path.Combine(_rootPath, $"{fileId}.bin");
    }
}
