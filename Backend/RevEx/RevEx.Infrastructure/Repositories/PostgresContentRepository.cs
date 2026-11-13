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
        return Query().FirstOrDefaultAsync(entity => entity.Cid == cid, token);
    }

    public override async Task<IReadOnlyCollection<Content>> GetAllAsync(CancellationToken token)
    {
        var entities = await Query().ToListAsync(token);
        PopulateTagIds(entities);
        return entities;
    }

    public override async Task<Content?> GetByIdAsync(int id, CancellationToken token)
    {
        var entity = await Query().FirstOrDefaultAsync(item => item.Id == id, token);
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
        var result = await base.AddAsync(entity, token);
        await ReplaceTagsAsync(result.Id, tagIds, token);
        result.TagIds = tagIds;
        return result;
    }

    public override async Task UpdateAsync(Content entity, CancellationToken token)
    {
        var tagIds = entity.TagIds.Distinct().ToArray();
        entity.Tags = [];
        await base.UpdateAsync(entity, token);
        await ReplaceTagsAsync(entity.Id, tagIds, token);
        entity.TagIds = tagIds;
    }

    private IQueryable<Content> Query()
    {
        return Context.Contents
            .AsNoTracking()
            .Include(entity => entity.Category)
            .Include(entity => entity.ContentStatus)
            .Include(entity => entity.Tags);
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
