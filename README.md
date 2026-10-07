# RevEx Web

RevEx Web combines the ASP.NET Core API, React frontend, Keycloak realm and local
Docker Compose infrastructure.

## Local setup

1. Copy `.env.example` to `.env`.
2. Replace every `change-me` value with a unique local value.
3. Start the stack:

   ```bash
   docker compose up -d --build
   ```

The application is available at:

- Frontend: <http://localhost:3000>
- API / Swagger: <http://localhost:8080/swagger>
- Keycloak: <http://localhost:8081>

For standalone API development, provide the PostgreSQL connection string through
the `ContentHistory__ConnectionString` environment variable.
