# Архитектура

## Компоненты и потоки

```mermaid
flowchart LR
    Web[React / nginx :3000] --> API[RevEx API :8082]
    Desktop[RevEx Desktop] --> API
    Web --> KC[Keycloak :8081]
    Desktop --> KC
    API --> Pg[(PostgreSQL)]
    API --> Kafka[Kafka]
    Kafka --> SMTP[SMTP notifications]
    API --> Files[Файлы и архивы релизов]
    Desktop --> Local[Локальные коннекторы]
```

Внутри Docker nginx обращается к `revex-api:8080`, а API загружает OIDC metadata
через `keycloak:8080`. Браузер и Desktop используют публичный issuer
`http://localhost:8081/realms/revex`. Эти адреса выполняют разные задачи.

## Backend: Backend/RevEx

| Проект | Назначение |
| --- | --- |
| `RevEx.Api` | Контроллеры, DI, Swagger, JWT, CORS, проверка совместимости desktop. |
| `RevEx.Application` | Абстракции persistence/storage; общие DTO находятся в Domain. |
| `RevEx.Domain` | Общие сущности, HTTP-контракты и роли из отдельного соседнего репозитория RevEx.Domain. |
| `RevEx.Infrastructure` | PostgreSQL EF Core-репозитории, файловые хранилища, релизы. |
| `tools/OpenApiExport` | Генерация спецификации из MVC-контроллеров без запуска инфраструктуры. |
| `tests/ApplicationChecks` | Быстрые проверки правил Application без внешней инфраструктуры. |
| `tests/ApiChecks` | Авторизованный HTTP CRUD на реальном PostgreSQL provider и SQL command-count assertions. |
| `tests/ContentSubscriptionChecks` | PostgreSQL+Kafka pipeline и идемпотентность уведомлений. |
| `tests/DesktopReleaseChecks` | Хранилище релизов, комментарии, миграция и Desktop tracking. |

Направление зависимостей: Api → Application/Infrastructure,
Infrastructure → Application/Domain, Application → Domain.
Контроллеры используют сервисы Application; репозитории реализуют `IRepository<T>`.

При старте `Startup.cs` применяет EF Core migration в scoped-контексте, затем добавляет
начальные данные. HTTP pipeline включает Swagger, CORS, authentication, authorization,
middleware совместимости и контроллеры. Hosted Kafka services стартуют после миграции;
поэтому PostgreSQL нужен до приёма запросов.

## Хранение

- PostgreSQL: каталог, метаданные файлов, комментарии, история контента, подписки,
  отметки доставки, fallback outbox, установки desktop, предложения обновлений и скачивания.
- `NetworkFolder`: содержимое файлов в Docker volume `revex-files`.
- `DesktopReleases:Path`: архивы и состояние релизов в `desktop-releases`.
- Альтернативные провайдеры файлов: S3 и Memory; Compose выбирает NetworkFolder.

Пользователи и роли принадлежат Keycloak, а не сущностям каталога.
Подробности связей и ограничений находятся в [ENTITIES.md](ENTITIES.md).

## Frontend

React + TypeScript + Vite; UI использует UIkit и lucide-react.
`src/main.tsx` — вход, `src/App.tsx` — состояние и навигация по pathname,
`src/api.ts` — HTTP-контракты, `src/auth.ts` — Keycloak,
`src/pages` — страницы, `src/components` — общие элементы.
Отдельной библиотеки маршрутизации нет.

Home публична и показывает загрузки desktop при опубликованном релизе.
Admin требует `administrator` и управляет релизами/аудитом; сервер проверяет права независимо от UI.
Отсутствие установщика до публикации релиза — ожидаемое состояние.

## Авторизация

Realm `revex`; публичные OIDC-клиенты `revex-desktop` и `revex-frontend`,
Authorization Code + PKCE S256. Audience API — `revex-api`, роли в claim `roles`.

| Роль | Доступ |
| --- | --- |
| `user` | Чтение каталога. |
| `moderator` | Чтение и изменение контента. |
| `administrator` | Контент, справочники, релизы и аудит. |

Иерархия realm: administrator → moderator → user.
Fallback policy требует вход и роль; отдельные методы релизов/обновлений публичны,
но могут дополнительно требовать ключ установки. См. [API.md](API.md).
ActiveDirectory/IIS предусмотрен кодом как отдельный режим; локальный Compose использует Keycloak.

## Согласованность и производительность

Все database-сущности проходят через EF Core. Каталожные записи и их связи защищены
внешними ключами, уникальными/check constraints и optimistic concurrency по `UpdatedAt`.
Content+tags и file+metadata сохраняются транзакционно. PostgreSQL не образует общей
транзакции с файловым storage, Kafka или SMTP; соответствующие сценарии используют
компенсацию, fallback outbox и idempotent delivery records.

Lazy loading отсутствует. Коллекции загружаются eager/projection-запросами, а пакетные
repository methods исключают запрос внутри цикла. Regression-проверки числа SQL-команд
описаны в [ENTITY_FRAMEWORK.md](ENTITY_FRAMEWORK.md).
