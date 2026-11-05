using RevEx.Application.Abstractions.Persistence;
using RevEx.Application.Abstractions.Storage;
using RevEx.Application.Content;
using RevEx.Domain.Entities;
using RevEx.Domain.Contracts.ContentFile;
using System.Text.Json;
namespace RevEx.Application.Catalog;
public sealed class ContentFilesService(IContentFileRepository repository, IFileStorage storage)
{
    public async Task<IReadOnlyCollection<ContentFileDto>> GetAll(CancellationToken token)
    {
        var files = await repository.GetAllAsync(token);
        return files.Select(Map).ToArray();
    }

    public async Task<ContentFileDto> GetById(int id, CancellationToken token)
    {
        var file = await Find(id, token);
        return Map(file);
    }

    public async Task<FileDownload> Download(int id, CancellationToken token)
    {
        var file = await Find(id, token);
        var stream = await storage.OpenReadAsync(id, token)
            ?? throw new ContentOperationException(ContentFailure.NotFound);
        return new FileDownload(stream, file.OriginalFileName);
    }

    private async Task<ContentFile> Find(int id, CancellationToken token)
    {
        return await repository.GetByIdAsync(id, token)
            ?? throw new ContentOperationException(ContentFailure.NotFound);
    }

    private static ContentFileDto Map(ContentFile file)
    {
        return new ContentFileDto(
            file.Id, file.Url, file.ContentVersionId, file.OriginalFileName, file.Extension,
            file.SizeBytes, file.Role, file.CreatedAt, file.UpdatedAt)
        {
            Metadata = file.MetadataJson is null ? null : JsonSerializer.Deserialize<JsonElement>(file.MetadataJson)
        };
    }
}
