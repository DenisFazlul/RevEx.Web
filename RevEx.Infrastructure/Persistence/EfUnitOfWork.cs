using RevEx.Application.Abstractions.Persistence;
using RevEx.Infrastructure.Data;

namespace RevEx.Infrastructure.Persistence;

public sealed class EfUnitOfWork(AppDbContext dbContext) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
