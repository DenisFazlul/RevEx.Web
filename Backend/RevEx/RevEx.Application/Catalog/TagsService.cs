using RevEx.Application.Abstractions.Persistence;
using RevEx.Domain.Contracts.Tag;
using RevEx.Domain.Entities;

using ContentEntity = RevEx.Domain.Entities.Content;
namespace RevEx.Application.Catalog;

using RevEx.Application.Content;

public sealed class TagsService(
    ITagRepository repository,
    IContentRepository contents,
    ITagGroupRepository groupRepository)
{

    public async Task<IReadOnlyCollection<TagDto>> GetAll(CancellationToken cancellationToken)
    {
        var entities = await repository.GetAllAsync(cancellationToken);
        return entities.Select(Map).ToArray();
    }

    public async Task<TagDto> GetById(int id, CancellationToken cancellationToken)
    {
        var entity = await repository.GetByIdAsync(id, cancellationToken);
        return entity is null ? throw new ContentOperationException(ContentFailure.NotFound) : Map(entity);
    }

    public async Task<TagDto> Create(CreateTagDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ContentOperationException(ContentFailure.Validation, "Name must not be empty.");
        }

        var name = request.Name.Trim();
        if (await groupRepository.GetByIdAsync(request.TagGroupId, cancellationToken) is null)
        {
            throw new ContentOperationException(ContentFailure.Validation, "TagGroupId must reference an existing tag group.");
        }
        if (await NameExists(name, null, cancellationToken))
        {
            throw new ContentOperationException(ContentFailure.Conflict, "A tag with this name already exists.");
        }

        var entity = new Tag { Name = name, TagGroupId = request.TagGroupId };
        entity.Color = NormalizeColor(request.Color) ?? Tag.DefaultColor;
        await repository.AddAsync(entity, cancellationToken);

        return Map(entity);
    }

    public async Task Update(int id, UpdateTagDto request, CancellationToken cancellationToken)
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
        if (await groupRepository.GetByIdAsync(request.TagGroupId, cancellationToken) is null)
        {
            throw new ContentOperationException(ContentFailure.Validation, "TagGroupId must reference an existing tag group.");
        }
        if (await NameExists(name, id, cancellationToken))
        {
            throw new ContentOperationException(ContentFailure.Conflict, "A tag with this name already exists.");
        }

        entity.Name = name;
        entity.TagGroupId = request.TagGroupId;
        entity.Color = NormalizeColor(request.Color) ?? entity.Color;
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

        await contents.RemoveTagFromAllAsync(id, cancellationToken);
        await repository.DeleteAsync(entity, cancellationToken);

        return;
    }

    private async Task<bool> NameExists(string name, int? excludedId, CancellationToken cancellationToken)
    {
        return await repository.NameExistsAsync(name, excludedId, cancellationToken);
    }

    private static string? NormalizeColor(string? color)
    {
        if (color is not null && (color.Length != 7 || color[0] != '#' || !color.Skip(1).All(Uri.IsHexDigit)))
        {
            throw new ContentOperationException(ContentFailure.Validation, "Color must use the #RRGGBB format.");
        }

        return color?.ToUpperInvariant();
    }

    private static TagDto Map(Tag entity) =>
        new(entity.Id, entity.Name, entity.TagGroupId, entity.TagGroup?.Name ?? string.Empty,
            entity.CreatedAt, entity.UpdatedAt, entity.Color);
}
