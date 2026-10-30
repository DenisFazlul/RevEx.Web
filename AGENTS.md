# RevEx Web guide

Scope: this RevEx.Web repository, including Backend/RevEx, Frontend, Keycloak and Compose.
This checkout has one Git root; do not assume Backend/RevEx is a nested repository.
Project documentation lives in root docs/ beside docker-compose.yml.

## Backend

| Path | Responsibility |
| --- | --- |
| `Backend/RevEx/RevEx.Api/` | Composition root, controllers, Swagger, JWT validation. |
| `Backend/RevEx/RevEx.Application/` | Content use cases and persistence/storage/identity abstractions. |
| `../RevEx.Domain/src/RevEx.Domain/` | Entities. |
| `Backend/RevEx/RevEx.Infrastructure/` | MongoDB repositories, PostgreSQL history, file storage and releases. |

Key files:

- `RevEx.Api/Program.cs` — DI, CORS, Swagger bearer support, JWT authentication, and
  the authenticated fallback policy.
- `RevEx.Api/Controllers/` — HTTP contract; inspect these before changing clients.
- `RevEx.Api/appsettings.json` — standalone issuer/audience settings.

Preserve dependency direction: `Api -> Application/Infrastructure`,
`Infrastructure -> Application/Domain`, and `Application -> Domain`. ContentController delegates to ContentService in Application. Other controllers still use
`IRepository<T>` and `IUnitOfWork`; do not inject `AppDbContext` into controllers.
Shared transport DTOs live in ../RevEx.Domain/src/RevEx.Contracts; Domain has no reference to Contracts.
Compose persists catalog metadata in MongoDB, history in PostgreSQL and files in named volumes.

All mapped controller endpoints are protected by default. Keep middleware order as
`UseCors`, `UseAuthentication`, `UseAuthorization`, then `MapControllers`.

Run from `WEB/Backend/RevEx/`:

```bash
dotnet build RevEx.sln
dotnet run --project RevEx.Api/RevEx.Api.csproj
```

## Frontend

- Entry: `Frontend/src/main.tsx`.
- State and pathname-based navigation: `Frontend/src/App.tsx`.
- API types and requests: `Frontend/src/api.ts`.
- Pages: `Frontend/src/pages/`; shared UI: `Frontend/src/components/ui.tsx`.
- Styling: `Frontend/src/styles.css`.

The frontend has no router library. Reuse existing patterns before adding dependencies
or a state-management layer. When API DTOs/routes change, update `src/api.ts` and every
caller. The API is now authenticated; account for bearer-token requirements when
working on live frontend requests.

Run from `WEB/Frontend/`:

```bash
npm run dev
npm run build
```

Vite uses port 5173 and proxies `/api` to `127.0.0.1:8080`.

## Keycloak and Compose

- Compose entry: `WEB/docker-compose.yml`.
- Realm import: `WEB/Keycloak/revex-realm.json`.
- Theme guide: `WEB/Keycloak/README.md`.
- Theme root: `WEB/Keycloak/themes/revex/login/`.

Services and host ports: Keycloak 8081, API 8082, frontend 3000. The API container listens on 8080. The API container uses
`http://keycloak:8080/...` to fetch metadata but validates tokens against the public
issuer `http://localhost:8081/realms/revex`. Do not collapse those two values without
testing issuer validation from both host and container.

Run from `WEB/`:

```bash
docker compose config --quiet
jq empty Keycloak/revex-realm.json
docker compose up -d --build
docker compose ps
docker compose logs --tail=100 keycloak revex-api
```

Realm import uses `--import-realm` and `IGNORE_EXISTING`. Updating the JSON does not
overwrite an already imported realm in a running container. In this development setup
Keycloak has no persistent volume, so force-recreating its container reimports it:

```bash
docker compose up -d --force-recreate keycloak
```

Do not use that assumption for a future persistent or production Keycloak database.

## Verification

- Backend change: build the backend solution.
- Frontend change: run the frontend production build.
- Contract change: build every affected client and server.
- Keycloak/theme change: validate Compose/JSON, restart Keycloak, request a real OIDC
  authorization URL with `prompt=login`, and check Keycloak logs for template errors.
- Never edit `node_modules`, `dist`, `bin`, `obj`, or `tsconfig.tsbuildinfo`.

## C# file layout

Keep one declared type per file, including records, enums, interfaces and test helpers. Name each file after its type. Keep top-level startup statements in Startup.cs and the public Program declaration in Program.cs.
