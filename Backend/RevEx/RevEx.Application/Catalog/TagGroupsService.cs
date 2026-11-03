using RevEx.Application.Abstractions.Persistence;
using RevEx.Domain.Contracts.Tag;
using RevEx.Domain.Entities;

namespace RevEx.Application.Catalog;
using RevEx.Application.Content;

public sealed class TagGroupsService(
    ITagGroupRepository repository,
    ITagRepository tagRepository)
{

    public async Task<IReadOnlyCollection<TagGroupDto>> GetAll(CancellationToken cancellationToken)
    {
        var entities = await repository.GetAllAsync(cancellationToken);
        return entities.Select(Map).ToArray();
    }

    public async Task<TagGroupDto> GetById(int id, CancellationToken cancellationToken)
    {
        var entity = await repository.GetByIdAsync(id, cancellationToken);
        return entity is null ? throw new ContentOperationException(ContentFailure.NotFound) : Map(entity);
    }

    public async Task<TagGroupDto> Create(CreateTagGroupDto request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name)) throw new ContentOperationException(ContentFailure.Validation, "Name must not be empty.");
        if (await NameExists(name, null, cancellationToken))
            throw new ContentOperationException(ContentFailure.Conflict, "A tag group with this name already exists.");

        var entity = new TagGroup { Name = name };
        await repository.AddAsync(entity, cancellationToken);

        return Map(entity);
    }

    public async Task Update(int id, UpdateTagGroupDto request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name)) throw new ContentOperationException(ContentFailure.Validation, "Name must not be empty.");
        var entity = await repository.GetByIdAsync(id, cancellationToken);
        if (entity is null) throw new ContentOperationException(ContentFailure.NotFound);
        if (await NameExists(name, id, cancellationToken))
            throw new ContentOperationException(ContentFailure.Conflict, "A tag group with this name already exists.");

        entity.Name = name;
        await repository.UpdateAsync(entity, cancellationToken);

        return;
    }

    public async Task Delete(int id, CancellationToken cancellationToken)
    {
        var entity = await repository.GetByIdAsync(id, cancellationToken);
        if (entity is null) throw new ContentOperationException(ContentFailure.NotFound);
        var tags = await tagRepository.GetAllAsync(cancellationToken);
        if (tags.Any(tag => tag.TagGroupId == id))
            throw new ContentOperationException(ContentFailure.Conflict, "Move or delete all tags in this group first.");

        await repository.DeleteAsync(entity, cancellationToken);

        return;
    }

    private async Task<bool> NameExists(string name, int? excludedId, CancellationToken cancellationToken)
    {
        var groups = await repository.GetAllAsync(cancellationToken);
        return groups.Any(item => item.Id != excludedId &&
            string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private static TagGroupDto Map(TagGroup entity) =>
        new(entity.Id, entity.Name, entity.CreatedAt, entity.UpdatedAt);
}
