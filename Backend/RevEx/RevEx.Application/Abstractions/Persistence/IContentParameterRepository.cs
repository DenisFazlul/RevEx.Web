using RevEx.Domain.Entities;

namespace RevEx.Application.Abstractions.Persistence;

public interface IContentParameterRepository : IRepository<ContentParameter>
{
    Task<IReadOnlyCollection<ContentParameter>> ListAsync(int contentId, CancellationToken token);
    Task<ContentParameter?> FindAsync(int contentId, int id, CancellationToken token);
}
