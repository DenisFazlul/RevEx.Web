# Разработка и проверки

## Backend

Требуется SDK, поддерживающий net8.0; локальная сборка также возможна SDK .NET 10.
В Docker используется SDK/runtime .NET 8.

```powershell
cd Backend/RevEx
dotnet build RevEx.sln
```

Для запуска отдельно от Compose нужны MongoDB, PostgreSQL и Keycloak:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = 'http://localhost:8082'
$env:ContentHistory__ConnectionString = 'Host=localhost;Port=5433;Database=revex_history;Username=revex;Password=<из .env>'
dotnet run --project RevEx.Api/RevEx.Api.csproj --no-launch-profile
```

Остановите контейнер API перед использованием того же порта. Для отдельного процесса
используйте публичный Authority; контейнерный MetadataAddress `keycloak:8080` на хосте не разрешается.
`appsettings.json` задаёт MongoDB localhost:27017, NetworkFolder и пустую обязательную строку истории.

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
dotnet run --project tests/DesktopReleaseChecks
# Из корня Web
docker compose config --quiet
```

`DesktopReleaseChecks` — исполняемая программа проверок, не проект для `dotnet test`.
Дополнительные PostgreSQL-проверки включаются через `REVEX_TEST_POSTGRES`.
HTTP-сценарии из `tests/DesktopReleaseChecks/http-checks.mjs` запускайте только
на одноразовом окружении, согласно [руководству релизов](DESKTOP_RELEASES.md).
Они могут изменять состояние релизов; не направляйте их на рабочие данные.

При изменениях backend выполните build; frontend — production build;
контрактов — сборки всех затронутых клиентов. Проверка Keycloak включает реальный
OIDC-вход с `prompt=login`, а не только чтение realm JSON.
Артефакты `bin`, `obj`, `node_modules`, `dist` не редактируются вручную.
