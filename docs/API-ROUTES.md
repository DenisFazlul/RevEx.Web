# Маршруты API

Список получен из docs/openapi.json, сгенерированного из текущих контроллеров.
Именованные политики и security следует проверять в самой спецификации;
отсутствие именованной политики не означает публичный доступ.

| Метод | Путь | Политика / доступ |
| --- | --- | --- |
| GET | `/api/categories` | Аутентификация; см. API.md |
| POST | `/api/categories` | AdministerCatalog |
| GET | `/api/categories/{id}` | Аутентификация; см. API.md |
| PUT | `/api/categories/{id}` | AdministerCatalog |
| DELETE | `/api/categories/{id}` | AdministerCatalog |
| GET | `/api/content` | Аутентификация; см. API.md |
| POST | `/api/content` | EditContent |
| GET | `/api/content/query` | Аутентификация; см. API.md |
| GET | `/api/content/{id}` | Аутентификация; см. API.md |
| PUT | `/api/content/{id}` | EditContent |
| DELETE | `/api/content/{id}` | EditContent |
| POST | `/api/content/{id}/preview` | EditContent |
| DELETE | `/api/content/{id}/preview` | EditContent |
| GET | `/api/content-files` | Аутентификация; см. API.md |
| GET | `/api/content-files/{id}` | Аутентификация; см. API.md |
| GET | `/api/content-files/{id}/content` | Аутентификация; см. API.md |
| GET | `/api/content/{contentId}/history` | Аутентификация; см. API.md |
| POST | `/api/content/{contentId}/history` | EditContent |
| GET | `/api/content-statuses` | Аутентификация; см. API.md |
| POST | `/api/content-statuses` | AdministerCatalog |
| GET | `/api/content-statuses/{id}` | Аутентификация; см. API.md |
| PUT | `/api/content-statuses/{id}` | AdministerCatalog |
| DELETE | `/api/content-statuses/{id}` | AdministerCatalog |
| GET | `/api/content-versions` | Аутентификация; см. API.md |
| POST | `/api/content-versions` | EditContent |
| GET | `/api/content-versions/{id}` | Аутентификация; см. API.md |
| PUT | `/api/content-versions/{id}` | EditContent |
| DELETE | `/api/content-versions/{id}` | EditContent |
| POST | `/api/content-versions/{id}/file` | EditContent |
| DELETE | `/api/content-versions/{id}/files/{fileId}` | EditContent |
| GET | `/api/desktop-releases` | AdministerCatalog |
| POST | `/api/desktop-releases` | AdministerCatalog |
| POST | `/api/desktop-releases/{version}/activate` | AdministerCatalog |
| PATCH | `/api/desktop-releases/{version}` | AdministerCatalog |
| DELETE | `/api/desktop-releases/{version}` | AdministerCatalog |
| POST | `/api/desktop-releases/{version}/restore` | AdministerCatalog |
| GET | `/api/desktop-release` | Аутентификация; см. API.md |
| GET | `/api/desktop-installers` | Аутентификация; см. API.md |
| GET | `/api/desktop-releases/{version}/files/{fileName}` | Аутентификация; см. API.md |
| POST | `/api/desktop-updates/check` | Аутентификация; см. API.md |
| POST | `/api/desktop-updates/report` | Аутентификация; см. API.md |
| GET | `/api/desktop-updates/history` | AdministerCatalog |
| POST | `/api/desktop-updates/offers/{id}/failure` | Аутентификация; см. API.md |
| GET | `/api/desktop-updates/offers/{id}/package` | Аутентификация; см. API.md |
| GET | `/api/tag-groups` | Аутентификация; см. API.md |
| POST | `/api/tag-groups` | AdministerCatalog |
| GET | `/api/tag-groups/{id}` | Аутентификация; см. API.md |
| PUT | `/api/tag-groups/{id}` | AdministerCatalog |
| DELETE | `/api/tag-groups/{id}` | AdministerCatalog |
| GET | `/api/tags` | Аутентификация; см. API.md |
| POST | `/api/tags` | AdministerCatalog |
| GET | `/api/tags/{id}` | Аутентификация; см. API.md |
| PUT | `/api/tags/{id}` | AdministerCatalog |
| DELETE | `/api/tags/{id}` | AdministerCatalog |
