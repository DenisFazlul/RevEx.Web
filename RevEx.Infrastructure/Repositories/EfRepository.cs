using Microsoft.EntityFrameworkCore;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Infrastructure.Data;

namespace RevEx.Infrastructure.Repositories;

public sealed class EfRepository<TEntity>(AppDbContext dbContext) : IRepository<TEntity>
    where TEntity : class
{
    private readonly DbSet<TEntity> _entities = dbContext.Set<TEntity>();

    public async Task<IReadOnlyCollection<TEntity>> GetAllAsync(CancellationToken cancellationToken) =>
        await _entities.AsNoTracking().ToListAsync(cancellationToken);

    public async Task<TEntity?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        await _entities.FindAsync([id], cancellationToken);

    public async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken)
    {
        await _entities.AddAsync(entity, cancellationToken);
        return entity;
    }

    public Task UpdateAsync(TEntity entity, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _entities.Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(TEntity entity, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _entities.Remove(entity);
        return Task.CompletedTask;
    }
}
