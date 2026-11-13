using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Domain.Entities;
using RevEx.Infrastructure.Persistence;

namespace RevEx.Infrastructure.Repositories;

public sealed class PostgresContentFileMetadataRepository(PostgresDbContext context)
    : PostgresRepository<ContentFileMetadata>(context), IContentFileMetadataRepository
{
    public Task<ContentFileMetadata?> GetByFileIdAsync(int fileId, CancellationToken token)
    {
        return Context.ContentFileMetadata
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.FileId == fileId, token);
    }

    public async Task<IReadOnlyCollection<ContentFileMetadata>> GetByFileIdsAsync(
        IReadOnlyCollection<int> fileIds,
        CancellationToken token)
    {
        if (fileIds.Count == 0)
        {
            return [];
        }

        var metadata = await Context.ContentFileMetadata
            .AsNoTracking()
            .Where(entity => fileIds.Contains(entity.FileId))
            .ToListAsync(token);
        return metadata;
    }

    public override async Task<ContentFileMetadata> AddAsync(
        ContentFileMetadata entity,
        CancellationToken token)
    {
        ValidateJson(entity.MetadataJson);
        var fileExists = await Context.ContentFiles
            .AsNoTracking()
            .AnyAsync(file => file.Id == entity.FileId, token);
        if (!fileExists)
        {
            throw new InvalidOperationException("Cannot create metadata for a file that does not exist.");
        }

        var result = await base.AddAsync(entity, token);
        return result;
    }

    public override async Task UpdateAsync(ContentFileMetadata entity, CancellationToken token)
    {
        ValidateJson(entity.MetadataJson);
        var existing = await GetByIdAsync(entity.Id, token)
            ?? throw new InvalidOperationException("File metadata was not found.");
        if (existing.FileId != entity.FileId)
        {
            throw new InvalidOperationException("File metadata cannot be assigned to another file.");
        }

        await base.UpdateAsync(entity, token);
    }

    private static void ValidateJson(string value)
    {
        using var document = JsonDocument.Parse(value);
    }
}
