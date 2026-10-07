namespace RevEx.Application.DTOs.Content;

public sealed class ContentQueryDto
{
    /// <summary>Категории, по которым фильтруется каталог.</summary>
    public int[] CategoryIds { get; init; } = [];

    /// <summary>Теги, каждый из которых должен быть назначен контенту.</summary>
    public int[] TagIds { get; init; } = [];

    /// <summary>Необязательная часть названия для поиска без учёта регистра.</summary>
    public string? Name { get; init; }
}
