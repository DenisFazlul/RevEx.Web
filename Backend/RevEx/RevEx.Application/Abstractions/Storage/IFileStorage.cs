namespace RevEx.Application.Abstractions.Storage;

public interface IFileStorage
{
    Task SaveAsync(int fileId, Stream content, CancellationToken cancellationToken);
    Task<Stream?> OpenReadAsync(int fileId, CancellationToken cancellationToken);
    Task DeleteAsync(int fileId, CancellationToken cancellationToken);
}
