using Microsoft.EntityFrameworkCore;
using Npgsql;
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
        var originalUpdatedAt = entity.UpdatedAt;
        Context.Attach(entity);
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        var entry = Context.Entry(entity);
        entry.State = EntityState.Modified;
        if (entity is Content)
        {
            entry.Property(nameof(Content.IsFrozen)).IsModified = false;
        }
        entry.Property(item => item.UpdatedAt).OriginalValue = originalUpdatedAt;
        await SaveAsync(token);
    }

    public virtual async Task DeleteAsync(TEntity entity, CancellationToken token)
    {
        Context.Entry(entity).State = EntityState.Deleted;
        await SaveAsync(token);
    }

    protected async Task SaveAsync(CancellationToken token)
    {
        try
        {
            await Context.SaveChangesAsync(token);
            Context.ChangeTracker.Clear();
        }
        catch (DbUpdateConcurrencyException exception)
        {
            Context.ChangeTracker.Clear();
            throw new PersistenceConflictException(
                "The record was changed or deleted by another request.",
                exception);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException postgresException &&
            postgresException.SqlState is PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ForeignKeyViolation)
        {
            Context.ChangeTracker.Clear();
            throw new PersistenceConflictException(
                "The operation conflicts with existing or related data.",
                exception);
        }
    }
}
