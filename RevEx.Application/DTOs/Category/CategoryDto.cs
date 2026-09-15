namespace RevEx.Application.DTOs.Category;

public sealed record CategoryDto(
    /* Идентификатор категории. */ int Id,
    /* Название категории. */ string Name,
    /* Дата создания категории. */ DateTimeOffset CreatedAt,
    /* Дата последнего изменения категории. */ DateTimeOffset UpdatedAt);
