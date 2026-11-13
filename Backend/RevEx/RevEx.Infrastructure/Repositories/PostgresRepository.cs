using Microsoft.EntityFrameworkCore;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Domain.Entities;
using RevEx.Infrastructure.Persistence;

namespace RevEx.Infrastructure.Repositories;

public abstract class PostgresRepository<TEntity>(PostgresDbContext context) : IRepository<TEntity>
    where TEntity : AuditableEntity
{
    protected PostgresDbContext Context { get; } = context;

    public virtual async Task<IReadOnlyCollection<TEntity>> GetAllAsync(CancellationToken token)
    {
        var entities = await Context.Set<TEntity>().AsNoTracking().ToListAsync(token);
        return entities;
    }

    public virtual Task<TEntity?> GetByIdAsync(int id, CancellationToken token)
    {
        return Context.Set<TEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => EF.Property<int>(entity, "Id") == id, token);
    }

    public virtual async Task<TEntity> AddAsync(TEntity entity, CancellationToken token)
    {
        var now = DateTimeOffset.UtcNow;
        entity.CreatedAt = now;
        entity.UpdatedAt = now;
        Context.Entry(entity).State = EntityState.Added;
        await SaveAsync(token);
        return entity;
    }

    public virtual async Task UpdateAsync(TEntity entity, CancellationToken token)
    {
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        Context.Entry(entity).State = EntityState.Modified;
        await SaveAsync(token);
    }

    public virtual async Task DeleteAsync(TEntity entity, CancellationToken token)
    {
        Context.Entry(entity).State = EntityState.Deleted;
        await SaveAsync(token);
    }

    protected async Task SaveAsync(CancellationToken token)
    {
        await Context.SaveChangesAsync(token);
        Context.ChangeTracker.Clear();
    }
}
