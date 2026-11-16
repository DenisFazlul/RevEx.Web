using Microsoft.EntityFrameworkCore;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Domain.Entities;
using RevEx.Infrastructure.Persistence;

namespace RevEx.Infrastructure.Repositories;

public sealed class PostgresContentRepository(PostgresDbContext context)
    : PostgresRepository<Content>(context), IContentRepository
{
    public Task<Content?> GetByCidAsync(Guid cid, CancellationToken token)
    {
        return Context.Contents
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Cid == cid, token);
    }

    public Task<bool> AnyWithCategoryIdAsync(int categoryId, CancellationToken token)
    {
        return Context.Contents
            .AsNoTracking()
            .AnyAsync(entity => entity.CategoryId == categoryId, token);
    }

    public Task<bool> AnyWithStatusIdAsync(int statusId, CancellationToken token)
    {
        return Context.Contents
            .AsNoTracking()
            .AnyAsync(entity => entity.ContentStatusId == statusId, token);
    }

    public async Task RemoveTagFromAllAsync(int tagId, CancellationToken token)
    {
        var contents = await Context.Contents
            .Where(entity => entity.Tags.Any(tag => tag.Id == tagId))
            .Include(entity => entity.Tags)
            .ToListAsync(token);

        foreach (var content in contents)
        {
            var tag = content.Tags.Single(entity => entity.Id == tagId);
            content.Tags.Remove(tag);
        }

        await SaveAsync(token);
    }

    public override async Task<IReadOnlyCollection<Content>> GetAllAsync(CancellationToken token)
    {
        var entities = await Context.Contents
            .AsNoTracking()
            .Include(entity => entity.Tags)
            .ToListAsync(token);
        PopulateTagIds(entities);
        return entities;
    }

    public override async Task<Content?> GetByIdAsync(int id, CancellationToken token)
    {
        var entity = await Context.Contents
            .AsNoTracking()
            .Include(item => item.Category)
            .Include(item => item.ContentStatus)
            .Include(item => item.Tags)
            .FirstOrDefaultAsync(item => item.Id == id, token);
        if (entity is not null)
        {
            PopulateTagIds([entity]);
        }

        return entity;
    }

    public override async Task<Content> AddAsync(Content entity, CancellationToken token)
    {
        var tagIds = entity.TagIds.Distinct().ToArray();
        entity.Tags = [];
        await using var transaction = await Context.Database.BeginTransactionAsync(token);
        try
        {
            var result = await base.AddAsync(entity, token);
            await ReplaceTagsAsync(result.Id, tagIds, token);
            await transaction.CommitAsync(token);
            result.TagIds = tagIds;
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            Context.ChangeTracker.Clear();
            throw;
        }
    }

    public override async Task UpdateAsync(Content entity, CancellationToken token)
    {
        var tagIds = entity.TagIds.Distinct().ToArray();
        entity.Tags = [];
        await using var transaction = await Context.Database.BeginTransactionAsync(token);
        try
        {
            await base.UpdateAsync(entity, token);
            await ReplaceTagsAsync(entity.Id, tagIds, token);
            await transaction.CommitAsync(token);
            entity.TagIds = tagIds;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            Context.ChangeTracker.Clear();
            throw;
        }
    }

    private async Task ReplaceTagsAsync(int contentId, IReadOnlyCollection<int> tagIds, CancellationToken token)
    {
        var content = await Context.Contents
            .Include(entity => entity.Tags)
            .SingleAsync(entity => entity.Id == contentId, token);
        var tags = tagIds.Count == 0
            ? []
            : await Context.Tags.Where(entity => tagIds.Contains(entity.Id)).ToListAsync(token);
        content.Tags.Clear();
        foreach (var tag in tags)
        {
            content.Tags.Add(tag);
        }

        await Context.SaveChangesAsync(token);
        Context.ChangeTracker.Clear();
    }

    private static void PopulateTagIds(IEnumerable<Content> entities)
    {
        foreach (var entity in entities)
        {
            entity.TagIds = entity.Tags.Select(tag => tag.Id).ToArray();
        }
    }
}
