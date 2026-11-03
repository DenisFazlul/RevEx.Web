using Microsoft.EntityFrameworkCore;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Domain.Entities;
using RevEx.Infrastructure.Persistence;

namespace RevEx.Infrastructure.Repositories;

/// <summary>
/// Common document CRUD with immediate persistence. Concrete repositories can override
/// operations to load relations or implement entity-specific persistence behavior.
/// </summary>
public abstract class MongoRepository<TEntity>(MongoDbContext context, Action<TEntity, int> assignId)
    : IRepository<TEntity> where TEntity : AuditableEntity
{
    protected MongoDbContext Context { get; } = context;

    public virtual async Task<IReadOnlyCollection<TEntity>> GetAllAsync(CancellationToken token) =>
        await Context.Set<TEntity>().AsNoTracking().ToListAsync(token);

    public virtual Task<TEntity?> GetByIdAsync(int id, CancellationToken token) =>
        Context.Set<TEntity>().AsNoTracking().FirstOrDefaultAsync(entity => EF.Property<int>(entity, "Id") == id, token);

    public virtual async Task<TEntity> AddAsync(TEntity entity, CancellationToken token)
    {
        assignId(entity, await Context.NextIdAsync<TEntity>(token));
        var now = DateTimeOffset.UtcNow;
        entity.CreatedAt = now;
        entity.UpdatedAt = now;
        Context.Add(entity);
        await SaveAsync(token);
        return entity;
    }

    public virtual async Task UpdateAsync(TEntity entity, CancellationToken token)
    {
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        Context.Update(entity);
        await SaveAsync(token);
    }

    public virtual async Task DeleteAsync(TEntity entity, CancellationToken token)
    {
        Context.Remove(entity);
        await SaveAsync(token);
    }

    protected async Task SaveAsync(CancellationToken token)
    {
        // The standalone MongoDB deployment uses immediate writes without transactions.
        Context.Database.AutoTransactionBehavior = AutoTransactionBehavior.Never;
        await Context.SaveChangesAsync(token);
        Context.ChangeTracker.Clear();
    }
}
