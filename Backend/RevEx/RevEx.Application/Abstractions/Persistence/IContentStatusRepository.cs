using RevEx.Domain.Entities;

namespace RevEx.Application.Abstractions.Persistence;

public interface IContentStatusRepository : IRepository<ContentStatus>
{
    Task<ContentStatus?> GetByNameAsync(string name, CancellationToken token);

    Task<bool> NameExistsAsync(string name, int? excludedId, CancellationToken token);
}
