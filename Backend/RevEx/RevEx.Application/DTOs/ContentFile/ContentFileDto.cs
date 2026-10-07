namespace RevEx.Application.DTOs.ContentFile;

public sealed record ContentFileDto(
    /* Идентификатор файла. */ int Id,
    /* API-адрес для скачивания файла. */ string Url,
    /* Идентификатор версии контента; отсутствует у превью карточки. */ int? ContentVersionId,
    /* Исходное имя загруженного файла. */ string OriginalFileName,
    /* Нормализованное расширение файла. */ string Extension,
    /* Размер файла в байтах. */ long SizeBytes,
    /* Назначение файла в версии. */ string Role,
    /* Дата создания записи. */ DateTimeOffset CreatedAt,
    /* Дата последнего изменения записи. */ DateTimeOffset UpdatedAt);
