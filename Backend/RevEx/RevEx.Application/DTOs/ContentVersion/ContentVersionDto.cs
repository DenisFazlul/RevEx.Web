namespace RevEx.Application.DTOs.ContentVersion;

public sealed record ContentVersionDto(
    /* Идентификатор версии. */ int Id,
    /* Идентификатор родительского контента. */ int ContentId,
    /* Идентификаторы файлов, входящих в версию. */ IReadOnlyCollection<int> FileIds,
    /* Пользовательское название версии. */ string Name,
    /* Дата выпуска или создания версии. */ DateTimeOffset Date,
    /* Состояние версии. */ string Status,
    /* Приложение, для которого предназначена версия. */ string Application,
    /* Версия целевого приложения. */ string ApplicationVersion,
    /* Дата создания записи. */ DateTimeOffset CreatedAt,
    /* Дата последнего изменения записи. */ DateTimeOffset UpdatedAt);
