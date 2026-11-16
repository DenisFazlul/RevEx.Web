# Разработка и проверки

## Backend

Требуется SDK, поддерживающий net8.0; локальная сборка также возможна SDK .NET 10.
В Docker используется SDK/runtime .NET 8.

```powershell
cd Backend/RevEx
dotnet build RevEx.sln
```

Для запуска отдельно от Compose нужны PostgreSQL, Keycloak и доступный Kafka broker:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = 'http://localhost:8082'
$env:Database__ConnectionString = 'Host=localhost;Port=5433;Database=revex;Username=revex;Password=<из .env>'
$env:Kafka__BootstrapServers = 'localhost:9092'
$env:Kafka__Topic = 'content-changes'
dotnet run --project RevEx.Api/RevEx.Api.csproj --no-launch-profile
```

Остановите контейнер API перед использованием того же порта. Для отдельного процесса
используйте публичный Authority; контейнерный MetadataAddress `keycloak:8080` на хосте не разрешается.
`appsettings.json` задаёт NetworkFolder и пустую обязательную строку PostgreSQL.
Опции БД проверяются при старте. Compose Kafka не публикует host-порт; для standalone
API используйте отдельный broker либо добавьте корректный внешний listener в локальном override.

## EF Core

Текущая схема — одна `InitialPostgres`. Проверка, что модель и snapshot совпадают:

```powershell
# Из Backend/RevEx
$env:Database__ConnectionString = 'Host=localhost;Port=5433;Database=revex;Username=revex;Password=<password>'
dotnet ef migrations has-pending-model-changes `
  --project RevEx.Infrastructure `
  --startup-project RevEx.Infrastructure `
  --context PostgresDbContext
```

Подробности ограничений, production deployment и N+1 — в
[ENTITY_FRAMEWORK.md](ENTITY_FRAMEWORK.md). Не редактируйте generated migration вручную.

## Frontend

Node.js 22 (как в Dockerfile), npm и lockfile репозитория:

```powershell
cd Frontend
npm ci
npm run dev
npm run build
```

Vite слушает 5173. В исходном `vite.config.ts` proxy `/api` направлен на
`127.0.0.1:8080`; для текущего Compose замените target на `http://127.0.0.1:8082`.
Это отдельная настройка от Docker nginx, где proxy уже использует `revex-api:8080`.

Frontend `.env.example` описывает build-time переменные.
`VITE_DESKTOP_API_URL` (fallback `VITE_API_URL`) задаёт отображаемый адрес подключения:
для текущего хоста используйте `http://localhost:8082/`.
`VITE_KEYCLOAK_URL` задаёт ссылку на консоль, `VITE_KEYCLOAK_BASE_URL` — базовый URL OIDC.
После смены переменных нужна пересборка. Для другого хоста необходимо согласовать
issuer, redirect URIs, web origins и CORS; простой замены localhost только в интерфейсе недостаточно.

## Документация API

Из `Backend/RevEx`:

```powershell
dotnet run --project tools/OpenApiExport
```

Результат по умолчанию — **корневой** `RevEx.Web/docs/openapi.json`.
Можно передать ровно один аргумент — абсолютный или относительный путь результата.
Генератор строит только MVC/Swagger: базы и Keycloak не нужны.
После изменения DTO/контроллеров обновите спецификацию, [API](API.md),
[сущности](ENTITIES.md), frontend `src/api.ts` и затронутые клиенты Desktop.

## Проверки

```powershell
# Из Backend/RevEx
dotnet run --project tests/ApplicationChecks
$env:REVEX_TEST_POSTGRES = 'Host=localhost;Port=5433;Database=revex;Username=revex;Password=<password>'
dotnet run --project tests/ApiChecks
dotnet run --project tests/DesktopReleaseChecks
# При доступных PostgreSQL и Kafka:
powershell -File tests/subscriptions-checks.ps1
# Из корня Web
docker compose config --quiet
```

`DesktopReleaseChecks` — исполняемая программа проверок, не проект для `dotnet test`.
`Database__ConnectionString` обязателен для design-time команд EF Core.
Для integration-проверок обязателен `REVEX_TEST_POSTGRES`; для subscriptions также
`REVEX_TEST_KAFKA`. Checks создают временные схемы и удаляют их в `finally`.
`ApiChecks` проверяет авторизованный HTTP CRUD, ограничения PostgreSQL, optimistic
concurrency, транзакционный rollback и верхние границы числа SQL-команд против N+1.
HTTP-сценарии из `tests/DesktopReleaseChecks/http-checks.mjs` запускайте только
на одноразовом окружении, согласно [руководству релизов](DESKTOP_RELEASES.md).
Они могут изменять состояние релизов; не направляйте их на рабочие данные.

При изменениях backend выполните build; frontend — production build;
контрактов — сборки всех затронутых клиентов. Проверка Keycloak включает реальный
OIDC-вход с `prompt=login`, а не только чтение realm JSON.
Артефакты `bin`, `obj`, `node_modules`, `dist` не редактируются вручную.
