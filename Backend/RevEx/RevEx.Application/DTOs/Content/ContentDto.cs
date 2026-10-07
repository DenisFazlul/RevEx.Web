namespace RevEx.Application.DTOs.Content;

public sealed record ContentDto(
    /* Идентификатор контента. */ int Id,
    /* Название контента. */ string Name,
    /* Описание контента. */ string Description,
    /* Идентификатор категории. */ int CategoryId,
    /* Идентификатор состояния карточки контента. */ int ContentStatusId,
    /* Идентификатор файла превью. */ int? PreviewFileId,
    /* Идентификаторы назначенных тегов. */ IReadOnlyCollection<int> TagIds,
    /* Дата создания контента. */ DateTimeOffset CreatedAt,
    /* Дата последнего изменения контента. */ DateTimeOffset UpdatedAt);
