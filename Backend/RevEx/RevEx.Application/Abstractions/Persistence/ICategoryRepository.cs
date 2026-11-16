using RevEx.Domain.Entities;

namespace RevEx.Application.Abstractions.Persistence;

public interface ICategoryRepository : IRepository<Category>
{
    Task<Category?> GetByNameAsync(string name, CancellationToken token);
}
