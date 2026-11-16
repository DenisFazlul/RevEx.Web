using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Domain.Entities;
using RevEx.Infrastructure.Persistence;

namespace RevEx.Infrastructure.Repositories;

public sealed class PostgresContentFileRepository(
    PostgresDbContext context,
    IContentFileMetadataRepository metadataRepository)
    : PostgresRepository<ContentFile>(context), IContentFileRepository
{
    public async Task<IReadOnlyCollection<ContentFile>> GetByContentVersionIdAsync(
        int contentVersionId,
        CancellationToken token)
    {
        var files = await Context.ContentFiles
            .AsNoTracking()
            .Where(file => file.ContentVersionId == contentVersionId)
            .ToListAsync(token);
        await PopulateMetadataAsync(files, token);
        return files;
    }

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

        await using var transaction = await Context.Database.BeginTransactionAsync(token);
        try
        {
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

            await transaction.CommitAsync(token);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            Context.ChangeTracker.Clear();
            throw;
        }
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
