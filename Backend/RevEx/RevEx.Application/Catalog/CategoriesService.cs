using RevEx.Application.Abstractions.Persistence;
using RevEx.Domain.Contracts.Category;
using RevEx.Domain.Entities;
using ContentEntity = RevEx.Domain.Entities.Content;

namespace RevEx.Application.Catalog;
using RevEx.Application.Content;

public sealed class CategoriesService(
    ICategoryRepository categoryRepository,
    IContentRepository contentRepository)
{

    public async Task<IReadOnlyCollection<CategoryDto>> GetAll(
        CancellationToken cancellationToken)
    {
        var entities = await categoryRepository.GetAllAsync(cancellationToken);
        return entities.Select(Map).ToArray();
    }

    public async Task<CategoryDto> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var entity = await categoryRepository.GetByIdAsync(id, cancellationToken);
        return entity is null ? throw new ContentOperationException(ContentFailure.NotFound) : Map(entity);
    }

    public async Task<CategoryDto> Create(
        CreateCategoryDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ContentOperationException(ContentFailure.Validation, "Name must not be empty.");
        }

        var entity = new Category { Name = request.Name.Trim() };
        await categoryRepository.AddAsync(entity, cancellationToken);

        var result = Map(entity);
        return result;
    }

    public async Task Update(
        int id,
        UpdateCategoryDto request,
        CancellationToken cancellationToken)
    {
        if (id == 1)
        {
            throw new ContentOperationException(ContentFailure.Conflict, "The base category 'Общие' cannot be changed.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ContentOperationException(ContentFailure.Validation, "Name must not be empty.");
        }

        var entity = await categoryRepository.GetByIdAsync(id, cancellationToken);
        if (entity is null)
        {
            throw new ContentOperationException(ContentFailure.NotFound);
        }

        entity.Name = request.Name.Trim();
        await categoryRepository.UpdateAsync(entity, cancellationToken);

        return;
    }

    public async Task Delete(int id, CancellationToken cancellationToken)
    {
        if (id == 1)
        {
            throw new ContentOperationException(ContentFailure.Conflict, "The base category 'Общие' cannot be deleted.");
        }

        var entity = await categoryRepository.GetByIdAsync(id, cancellationToken);
        if (entity is null)
        {
            throw new ContentOperationException(ContentFailure.NotFound);
        }

        var contents = await contentRepository.GetAllAsync(cancellationToken);
        if (contents.Any(content => content.CategoryId == id))
        {
            throw new ContentOperationException(ContentFailure.Conflict, "The category is used by content and cannot be deleted.");
        }

        await categoryRepository.DeleteAsync(entity, cancellationToken);

        return;
    }

    private static CategoryDto Map(Category entity) =>
        new(entity.Id, entity.Name, entity.CreatedAt, entity.UpdatedAt);
}
