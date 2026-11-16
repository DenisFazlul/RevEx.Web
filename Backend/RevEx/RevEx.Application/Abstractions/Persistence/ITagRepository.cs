using RevEx.Domain.Entities;

namespace RevEx.Application.Abstractions.Persistence;

public interface ITagRepository : IRepository<Tag>
{
    Task<IReadOnlyCollection<Tag>> GetByIdsAsync(
        IReadOnlyCollection<int> ids,
        CancellationToken token);

    Task<bool> AnyInGroupAsync(int tagGroupId, CancellationToken token);

    Task<bool> NameExistsAsync(string name, int? excludedId, CancellationToken token);
}
