# Границы бэкенда

API отвечает за HTTP, авторизацию, multipart и сборку зависимостей. Application
выполняет сценарии каталога, версий, файлов, истории и подписок. HTTP-ошибки
преобразуются фильтрами API; Application не зависит от ASP.NET Core или Infrastructure.

Application/Abstractions содержит интерфейсы хранения и `ICurrentUser`. Domain
содержит сущности и перечисления, Contracts — общие DTO. Domain не зависит от Contracts.

`IContentQueries` реализован `PostgresContentQueries`: фильтрация по категориям,
всем выбранным тегам и части названия выполняется в PostgreSQL через EF Core.
Offset и Limit необязательны; сортировка по Id обеспечивает устойчивый порядок.

## Организация Infrastructure

- `Persistence/` — единый `PostgresDbContext`, модель, фабрика и стартовая миграция.
- `Repositories/` — EF Core-репозитории каталога и специализированные PostgreSQL stores.
- `Messaging/` — публикация событий и очередь Kafka.
- `Notifications/` — подписки и доставка уведомлений.

Каталожные реализации наследуются от `PostgresRepository<TEntity>`, который
реализует общий CRUD и сохраняет каждую мутацию немедленно. Специализированные
репозитории загружают необходимые связи. `PostgresContentRepository` синхронизирует
many-to-many связь тегов, а `PostgresContentFileMetadataRepository` хранит
динамические метаданные как JSONB с уникальным `FileId`.

Query methods не возвращают `IQueryable` за границу Infrastructure. Проверки
существования используют `AnyAsync`, а связанные наборы — пакетные методы (`GetByIds`,
`GetByContentId`, `GetByContentVersionId`). Это сохраняет SQL внутри persistence boundary
и не позволяет Application случайно создать N+1.

API зависит от интерфейсов Application; контекст не передаётся контроллерам.
Публичного UnitOfWork нет. Внутри одного repository operation EF-транзакции объединяют
content+tags и file+metadata. Несколько application-операций, файловое хранилище,
история, Kafka и SMTP не образуют общей транзакции, поэтому сервисы явно выполняют
компенсацию и идемпотентные повторения там, где это требуется.

`UpdatedAt` — concurrency token для каталожных `AuditableEntity`. Stale update и
database constraint conflict преобразуются в application exception и HTTP 409.

## Проверка

Основные команды:

```powershell
dotnet build Backend/RevEx/RevEx.sln
dotnet run --project Backend/RevEx/tests/ApplicationChecks
$env:REVEX_TEST_POSTGRES = 'Host=localhost;Port=5433;Database=revex;Username=revex;Password=<password>'
dotnet run --project Backend/RevEx/tests/ApiChecks
dotnet run --project Backend/RevEx/tests/DesktopReleaseChecks
docker compose up -d --build
```

`ApiChecks` создаёт временную схему PostgreSQL, применяет стартовую миграцию и
проверяет авторизованный HTTP CRUD, поиск, ограничения и транзакционный rollback через
настоящий EF Core provider. SQL interceptor контролирует фиксированное число команд для
списка content, content с большим числом тегов и удаления связанного тега.
