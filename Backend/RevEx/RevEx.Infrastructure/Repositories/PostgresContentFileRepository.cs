using System.Text.Json;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Domain.Entities;
using RevEx.Infrastructure.Persistence;

namespace RevEx.Infrastructure.Repositories;

public sealed class PostgresContentFileRepository(
    PostgresDbContext context,
    IContentFileMetadataRepository metadataRepository)
    : PostgresRepository<ContentFile>(context), IContentFileRepository
{
    public override async Task<IReadOnlyCollection<ContentFile>> GetAllAsync(CancellationToken token)
    {
        var files = await base.GetAllAsync(token);
        await PopulateMetadataAsync(files, token);
        return files;
    }

    public override async Task<ContentFile?> GetByIdAsync(int id, CancellationToken token)
    {
        var file = await base.GetByIdAsync(id, token);
        if (file is not null)
        {
            await PopulateMetadataAsync([file], token);
        }

        return file;
    }

    public override async Task<ContentFile> AddAsync(ContentFile entity, CancellationToken token)
    {
        if (entity.MetadataJson is not null)
        {
            using var document = JsonDocument.Parse(entity.MetadataJson);
        }

        var result = await base.AddAsync(entity, token);
        if (entity.MetadataJson is not null)
        {
            var metadata = new ContentFileMetadata
            {
                FileId = result.Id,
                MetadataJson = entity.MetadataJson
            };
            await metadataRepository.AddAsync(metadata, token);
        }

        return result;
    }

    private async Task PopulateMetadataAsync(
        IReadOnlyCollection<ContentFile> files,
        CancellationToken token)
    {
        var fileIds = files.Select(file => file.Id).ToArray();
        var metadata = await metadataRepository.GetByFileIdsAsync(fileIds, token);
        var metadataByFileId = metadata.ToDictionary(entity => entity.FileId);
        foreach (var file in files)
        {
            file.MetadataJson = metadataByFileId.GetValueOrDefault(file.Id)?.MetadataJson;
        }
    }
}
