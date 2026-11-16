using RevEx.Domain.Entities;

namespace RevEx.Application.Abstractions.Persistence;

public interface ITagGroupRepository : IRepository<TagGroup>
{
    Task<TagGroup?> GetByNameAsync(string name, CancellationToken token);

    Task<bool> NameExistsAsync(string name, int? excludedId, CancellationToken token);
}
