# Маршруты API

Список синхронизирован с `docs/openapi.json`, сгенерированным из текущих MVC-контроллеров
13.11.2026. `Authenticated` означает fallback policy и одну из API-ролей; именованные
политики описаны в [API.md](API.md). `Public` означает явный анонимный endpoint, но для
пакетов Desktop дополнительно может требоваться `X-RevEx-Installation-Key`.

| Метод | Путь | Доступ |
| --- | --- | --- |
| GET | `/api/categories` | Authenticated |
| POST | `/api/categories` | AdministerCatalog |
| GET | `/api/categories/{id}` | Authenticated |
| PUT | `/api/categories/{id}` | AdministerCatalog |
| DELETE | `/api/categories/{id}` | AdministerCatalog |
| GET | `/api/comment-sets` | Authenticated |
| POST | `/api/comment-sets` | Authenticated |
| GET | `/api/comment-sets/{setId}` | Authenticated |
| GET | `/api/comment-sets/{setId}/comments` | Authenticated |
| POST | `/api/comment-sets/{setId}/comments` | Authenticated |
| PUT | `/api/comment-sets/{setId}/comments/{id}` | Authenticated; только автор |
| DELETE | `/api/comment-sets/{setId}/comments/{id}` | Authenticated; только автор |
| GET | `/api/content` | Authenticated |
| POST | `/api/content` | EditContent |
| GET | `/api/content/query` | Authenticated |
| GET | `/api/content/{id}` | Authenticated |
| PUT | `/api/content/{id}` | EditContent |
| DELETE | `/api/content/{id}` | EditContent |
| POST | `/api/content/{id}/preview` | EditContent |
| DELETE | `/api/content/{id}/preview` | EditContent |
| GET | `/api/content-files` | Authenticated |
| GET | `/api/content-files/{id}` | Authenticated |
| GET | `/api/content-files/{id}/content` | Authenticated |
| GET | `/api/content/{contentId}/history` | Authenticated |
| POST | `/api/content/{contentId}/history` | EditContent |
| GET | `/api/content-statuses` | Authenticated |
| POST | `/api/content-statuses` | AdministerCatalog |
| GET | `/api/content-statuses/{id}` | Authenticated |
| PUT | `/api/content-statuses/{id}` | AdministerCatalog |
| DELETE | `/api/content-statuses/{id}` | AdministerCatalog |
| GET | `/api/content/{contentId}/subscription` | Authenticated |
| PUT | `/api/content/{contentId}/subscription` | Authenticated |
| DELETE | `/api/content/{contentId}/subscription` | Authenticated |
| GET | `/api/content-versions` | Authenticated |
| POST | `/api/content-versions` | EditContent |
| GET | `/api/content-versions/{id}` | Authenticated |
| PUT | `/api/content-versions/{id}` | EditContent |
| DELETE | `/api/content-versions/{id}` | EditContent |
| POST | `/api/content-versions/{id}/file` | EditContent |
| DELETE | `/api/content-versions/{id}/files/{fileId}` | EditContent |
| PUT | `/api/content-versions/{id}/publish-status` | EditContent |
| GET | `/api/desktop-releases` | AdministerCatalog |
| POST | `/api/desktop-releases` | AdministerCatalog |
| POST | `/api/desktop-releases/{version}/activate` | AdministerCatalog |
| PATCH | `/api/desktop-releases/{version}` | AdministerCatalog |
| DELETE | `/api/desktop-releases/{version}` | AdministerCatalog |
| POST | `/api/desktop-releases/{version}/restore` | AdministerCatalog |
| GET | `/api/desktop-release` | Public |
| GET | `/api/desktop-installers` | Public |
| GET | `/api/desktop-releases/{version}/files/{fileName}` | Public |
| POST | `/api/desktop-updates/check` | Public |
| POST | `/api/desktop-updates/report` | Authenticated |
| GET | `/api/desktop-updates/history` | AdministerCatalog |
| POST | `/api/desktop-updates/offers/{id}/failure` | Public; installation key |
| GET | `/api/desktop-updates/offers/{id}/package` | Public; installation key |
| POST | `/api/identifiers/uuid-v7` | EditContent |
| GET | `/api/revit-releases` | AdministerCatalog |
| POST | `/api/revit-releases` | AdministerCatalog |
| GET | `/api/revit-releases/installers` | Public |
| POST | `/api/revit-releases/{id}/publish` | AdministerCatalog |
| GET | `/api/revit-releases/{id}/download` | Public |
| POST | `/api/imports/rfa` | EditContent |
| GET | `/api/tag-groups` | Authenticated |
| POST | `/api/tag-groups` | AdministerCatalog |
| GET | `/api/tag-groups/{id}` | Authenticated |
| PUT | `/api/tag-groups/{id}` | AdministerCatalog |
| DELETE | `/api/tag-groups/{id}` | AdministerCatalog |
| GET | `/api/tags` | Authenticated |
| POST | `/api/tags` | AdministerCatalog |
| GET | `/api/tags/{id}` | Authenticated |
| PUT | `/api/tags/{id}` | AdministerCatalog |
| DELETE | `/api/tags/{id}` | AdministerCatalog |
