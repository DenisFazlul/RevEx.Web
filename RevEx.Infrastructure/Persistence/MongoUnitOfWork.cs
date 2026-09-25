using RevEx.Application.Abstractions.Persistence;

namespace RevEx.Infrastructure.Persistence;

public sealed class MongoUnitOfWork : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(0);
    }
}
