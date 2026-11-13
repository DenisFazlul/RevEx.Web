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

API зависит от интерфейсов Application; контекст не передаётся контроллерам.
Пустого UnitOfWork нет. Несколько мутаций, файловое хранилище и история не образуют
общей транзакции, поэтому частичное выполнение между PostgreSQL и файлами возможно.

## Проверка

Основные команды:

```powershell
dotnet build Backend/RevEx/RevEx.sln
dotnet run --project Backend/RevEx/tests/ApplicationChecks
dotnet run --project Backend/RevEx/tests/ApiChecks
docker compose up -d --build
```

`ApiChecks` создаёт временную схему PostgreSQL, применяет стартовую миграцию и
проверяет авторизованный HTTP CRUD и поиск через настоящий EF Core provider.
