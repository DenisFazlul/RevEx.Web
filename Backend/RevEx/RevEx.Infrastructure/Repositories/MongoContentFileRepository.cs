using RevEx.Application.Abstractions.Persistence;
using RevEx.Domain.Entities;
using RevEx.Infrastructure.Persistence;
using MongoDB.Bson;

namespace RevEx.Infrastructure.Repositories;

public sealed class MongoContentFileRepository(MongoDbContext context, IContentFileMetadataRepository metadataRepository)
    : MongoRepository<ContentFile>(context, (entity, id) => entity.Id = id), IContentFileRepository
{
    public override async Task<IReadOnlyCollection<ContentFile>> GetAllAsync(CancellationToken token)
    {
        var files = await base.GetAllAsync(token);
        var metadata = await metadataRepository.GetByFileIdsAsync(files.Select(file => file.Id).ToArray(), token);
        var byFileId = metadata.ToDictionary(entity => entity.FileId,
            entity => string.IsNullOrEmpty(entity.MetadataJson) ? null : entity.MetadataJson);
        foreach (var file in files)
        {
            file.MetadataJson = byFileId.GetValueOrDefault(file.Id);
        }

        return files;
    }

    public override async Task<ContentFile?> GetByIdAsync(int id, CancellationToken token)
    {
        var file = await base.GetByIdAsync(id, token);
        if (file is not null)
        {
            var metadata = await metadataRepository.GetByFileIdAsync(file.Id, token);
            file.MetadataJson = string.IsNullOrEmpty(metadata?.MetadataJson) ? null : metadata.MetadataJson;
        }

        return file;
    }

    public override async Task<ContentFile> AddAsync(ContentFile entity, CancellationToken token)
    {
        // Parse before creating the record so invalid metadata cannot leave an orphan file.
        if (entity.MetadataJson is not null)
        {
            BsonDocument.Parse(entity.MetadataJson);
        }

        var result = await base.AddAsync(entity, token);
        if (entity.MetadataJson is not null)
        {
            try
            {
                await metadataRepository.AddAsync(new ContentFileMetadata
                {
                    FileId = entity.Id,
                    MetadataJson = entity.MetadataJson
                }, token);
            }
            catch
            {
                await base.DeleteAsync(entity, CancellationToken.None);
                throw;
            }
        }

        return result;
    }

    public override async Task DeleteAsync(ContentFile entity, CancellationToken token)
    {
        var metadata = await metadataRepository.GetByFileIdAsync(entity.Id, token);
        if (metadata is not null)
        {
            await metadataRepository.DeleteAsync(metadata, token);
        }

        await base.DeleteAsync(entity, token);
    }
}
