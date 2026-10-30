using RevEx.Domain.Contracts.Content;
using RevEx.Domain.Entities;
namespace RevEx.Application.Abstractions.Persistence;

public interface IContentQueries
{
    Task<IReadOnlyCollection<ContentDto>> QueryAsync(ContentQueryDto query, CancellationToken cancellationToken);
}
