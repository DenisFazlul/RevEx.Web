# RevEx Web guide

Scope: everything under `WEB/`. The backend Git root is `WEB/Backend/RevEx/`.
The frontend, Compose file, realm import, and Keycloak theme belong to the separate
`WEB/` Git repository; do not include them in nested backend commits. WEB also tracks
the backend files as a snapshot; commit backend changes in its nested repository
and then in WEB so both histories contain a consistent implementation.

## Backend

| Path | Responsibility |
| --- | --- |
| `Backend/RevEx/RevEx.Api/` | Composition root, controllers, Swagger, JWT validation. |
| `Backend/RevEx/RevEx.Application/` | DTOs and persistence/storage abstractions. |
| `Backend/RevEx/RevEx.Domain/` | Entities. |
| `Backend/RevEx/RevEx.Infrastructure/` | EF repositories, unit of work, in-memory DB/files. |

Key files:

- `RevEx.Api/Program.cs` — DI, CORS, Swagger bearer support, JWT authentication, and
  the authenticated fallback policy.
- `RevEx.Api/Controllers/` — HTTP contract; inspect these before changing clients.
- `RevEx.Api/appsettings.json` — standalone issuer/audience settings.

Preserve dependency direction: `Api -> Application/Infrastructure`,
`Infrastructure -> Application/Domain`, and `Application -> Domain`. Controllers use
`IRepository<T>` and `IUnitOfWork`; do not inject `AppDbContext` into controllers.
Persistence and uploaded files are intentionally in memory.

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

Services and ports: Keycloak 8081, API 8080, frontend 3000. The API container uses
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
