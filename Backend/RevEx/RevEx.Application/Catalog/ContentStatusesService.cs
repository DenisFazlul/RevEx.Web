using RevEx.Application.Abstractions.Persistence;
using RevEx.Domain.Contracts.ContentStatus;
using RevEx.Domain.Entities;
using ContentEntity = RevEx.Domain.Entities.Content;

namespace RevEx.Application.Catalog;
using RevEx.Application.Content;

public sealed class ContentStatusesService(
    IContentStatusRepository repository,
    IContentRepository contentRepository)
{

    public async Task<IReadOnlyCollection<ContentStatusDto>> GetAll(CancellationToken cancellationToken)
    {
        var entities = await repository.GetAllAsync(cancellationToken);
        return entities.Select(Map).ToArray();
    }

    public async Task<ContentStatusDto> GetById(int id, CancellationToken cancellationToken)
    {
        var entity = await repository.GetByIdAsync(id, cancellationToken);
        return entity is null ? throw new ContentOperationException(ContentFailure.NotFound) : Map(entity);
    }

    public async Task<ContentStatusDto> Create(
        CreateContentStatusDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ContentOperationException(ContentFailure.Validation, "Name must not be empty.");
        }

        var name = request.Name.Trim();
        var statuses = await repository.GetAllAsync(cancellationToken);
        if (statuses.Any(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ContentOperationException(ContentFailure.Conflict, "A content status with this name already exists.");
        }

        var entity = new ContentStatus { Name = name };
        await repository.AddAsync(entity, cancellationToken);

        return Map(entity);
    }

    public async Task Update(
        int id,
        UpdateContentStatusDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ContentOperationException(ContentFailure.Validation, "Name must not be empty.");
        }

        var entity = await repository.GetByIdAsync(id, cancellationToken);
        if (entity is null)
        {
            throw new ContentOperationException(ContentFailure.NotFound);
        }

        var name = request.Name.Trim();
        var statuses = await repository.GetAllAsync(cancellationToken);
        if (statuses.Any(item => item.Id != id && string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ContentOperationException(ContentFailure.Conflict, "A content status with this name already exists.");
        }

        entity.Name = name;
        await repository.UpdateAsync(entity, cancellationToken);

        return;
    }

    public async Task Delete(int id, CancellationToken cancellationToken)
    {
        var entity = await repository.GetByIdAsync(id, cancellationToken);
        if (entity is null)
        {
            throw new ContentOperationException(ContentFailure.NotFound);
        }

        var contents = await contentRepository.GetAllAsync(cancellationToken);
        if (contents.Any(content => content.ContentStatusId == id))
        {
            throw new ContentOperationException(ContentFailure.Conflict, "The status is used by content and cannot be deleted.");
        }

        await repository.DeleteAsync(entity, cancellationToken);

        return;
    }

    private static ContentStatusDto Map(ContentStatus entity) =>
        new(entity.Id, entity.Name, entity.CreatedAt, entity.UpdatedAt);
}
