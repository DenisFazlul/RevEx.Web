# PostgreSQL и Entity Framework Core

PostgreSQL — единственная база данных backend. Каталог, версии, файлы и их JSON-метаданные,
история, подписки, отметки доставки, Kafka outbox, комментарии и аудит Desktop используют
`PostgresDbContext`. Других database provider и прямых подключений к иной СУБД в backend нет.

## Конфигурация и время жизни

Строка подключения находится в `Database:ConnectionString`; для переменных окружения —
`Database__ConnectionString`. При старте options validation требует непустые `Host`, `Database`
и `Username`. В HTTP-сценариях `PostgresDbContext` регистрируется как scoped. Singleton stores и
background services не удерживают контекст: `PostgresContextFactory` создаёт короткоживущий
контекст на одну операцию.

Модель определяется в `Persistence/PostgresModel.cs`. Имена таблиц и столбцов — snake_case.
Каталожные чтения выполняются с `AsNoTracking`; обновления используют `UpdatedAt` как optimistic
concurrency token. Конфликты конкурентного обновления, уникальных и внешних ключей преобразуются
в `PersistenceConflictException`, а API возвращает 409 без деталей PostgreSQL.

## Схема и ограничения

- `citext` и уникальные индексы обеспечивают регистронезависимую уникальность названий категорий,
  состояний, тегов и групп тегов.
- CID и VID уникальны; связи защищены внешними ключами.
- Связь content–tag хранится в `content_tags`.
- Для версии разрешены только значения enum `Platform` и `PublishStatus` от 0 до 2.
- Размер файла не может быть отрицательным; в одной версии допускается один файл с ролью `primary`.
- Метаданные файла хранятся отдельно в `content_file_metadata.metadata_json` типа JSONB.
  Корень JSON обязан быть объектом; `file_id` уникален и удаляется каскадно вместе с файлом.
- Составные индексы покрывают историю, комментарии, подписки, outbox и аудит Desktop.

Операции content+tags и file+metadata выполняются в явных транзакциях EF Core. Файловое
хранилище, Kafka и SMTP не могут участвовать в транзакции PostgreSQL; сервисы используют
компенсирующее удаление, idempotency или outbox там, где это предусмотрено сценарием.

## Миграции

Текущая схема создаётся одной миграцией `InitialPostgres`; перенос прежних данных намеренно не
выполняется. `PostgresDbContextModelSnapshot` обязан совпадать с моделью. Backend Compose при старте
один раз вызывает `Database.MigrateAsync()` до seed и запуска hosted services. Repositories, stores
и background workers миграции не запускают и таблицы самостоятельно не создают.

Проверка модели из `Backend/RevEx`:

```powershell
$env:Database__ConnectionString = 'Host=localhost;Port=5433;Database=revex;Username=revex;Password=<password>'
dotnet ef migrations has-pending-model-changes `
  --project RevEx.Infrastructure `
  --startup-project RevEx.Infrastructure `
  --context PostgresDbContext
```

`PostgresDesignTimeFactory` не содержит fallback-подключения: перед любой design-time командой
EF Core задайте `Database__ConnectionString` явно.

Для production runtime-миграция несколькими репликами не рекомендуется. Выпуск должен применять
проверенный SQL script или migration bundle отдельным deployment step с отдельными правами на DDL,
после чего приложение запускается с обычной учётной записью. Runtime-вызов сохранён для текущего
однорепличного Compose/dev-окружения.

## Защита от N+1

Lazy loading не подключён. Нужные связи загружаются одним запросом через `Include` либо projection.
Пакетные методы репозиториев используются для тегов, версий, файлов и проверок существования;
RFA-import загружает версию один раз и добавляет набор файлов без повторного чтения версии.

`ApiChecks` содержит regression-проверки числа SQL-команд: список content выполняется одним SQL,
создание content с 20 тегами и удаление связанного тега имеют фиксированную верхнюю границу,
не зависящую от количества тегов или карточек. Циклы удаления файлов относятся к внешнему storage,
а SMTP-доставка фиксируется по одному получателю намеренно: это сохраняет уже успешные доставки при
частичном сбое и не является скрытым lazy-loading/N+1 чтением.

Официальные рекомендации: [DbContext lifetime](https://learn.microsoft.com/ef/core/dbcontext-configuration/),
[efficient querying](https://learn.microsoft.com/ef/core/performance/efficient-querying),
[transactions](https://learn.microsoft.com/ef/core/saving/transactions),
[applying migrations](https://learn.microsoft.com/ef/core/managing-schemas/migrations/applying) и
[Npgsql JSON mapping](https://www.npgsql.org/efcore/mapping/json.html).
