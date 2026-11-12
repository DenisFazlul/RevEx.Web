using RevEx.Domain.Entities;

namespace RevEx.Application.Abstractions.Persistence;

public interface IContentVersionRepository : IRepository<ContentVersion>
{
    Task<ContentVersion?> GetByVidAsync(Guid vid, CancellationToken token);
}
