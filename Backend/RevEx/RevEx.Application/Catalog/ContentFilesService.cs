using RevEx.Application.Abstractions.Persistence;
using RevEx.Application.Abstractions.Storage;
using RevEx.Application.Content;
using RevEx.Domain.Entities;
using RevEx.Domain.Contracts.ContentFile;
namespace RevEx.Application.Catalog;
public sealed class ContentFilesService(IContentFileRepository repository, IFileStorage storage)
{
 public async Task<IReadOnlyCollection<ContentFileDto>> GetAll(CancellationToken token) => (await repository.GetAllAsync(token)).Select(Map).ToArray();
 public async Task<ContentFileDto> GetById(int id, CancellationToken token) => Map(await Find(id, token));
 public async Task<FileDownload> Download(int id, CancellationToken token)
 {
  var file = await Find(id, token);
  var stream = await storage.OpenReadAsync(id, token) ?? throw new ContentOperationException(ContentFailure.NotFound);
  return new(stream, file.OriginalFileName);
 }
 private async Task<ContentFile> Find(int id, CancellationToken token) => await repository.GetByIdAsync(id, token) ?? throw new ContentOperationException(ContentFailure.NotFound);
 private static ContentFileDto Map(ContentFile e) => new(e.Id,e.Url,e.ContentVersionId,e.OriginalFileName,e.Extension,e.SizeBytes,e.Role,e.CreatedAt,e.UpdatedAt);
}
