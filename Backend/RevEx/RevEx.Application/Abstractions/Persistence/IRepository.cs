namespace RevEx.Application.Abstractions.Persistence;

/// <summary>
/// Common entity operations. Each mutation is persisted immediately; multiple calls are not a transaction.
/// Entity-specific operations belong to the specialized repository interfaces.
/// </summary>
public interface IRepository<TEntity> where TEntity : class
{
    Task<IReadOnlyCollection<TEntity>> GetAllAsync(CancellationToken token);
    Task<TEntity?> GetByIdAsync(int id, CancellationToken token);
    Task<TEntity> AddAsync(TEntity entity, CancellationToken token);
    Task UpdateAsync(TEntity entity, CancellationToken token);
    Task DeleteAsync(TEntity entity, CancellationToken token);
}
