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

## API documentation

See [OpenAPI and API usage](Backend/RevEx/docs/README.md) and
[entities and relationships](Backend/RevEx/docs/ENTITIES.md).
The versioned [OpenAPI JSON](Backend/RevEx/docs/openapi.json) can be imported into API clients.

## Сборки для Revit

В «Администрирование → Сборки для Revit» администратор загружает EXE, MSI или ZIP
(до 1 ГБ), указывает версию RevEx в формате `1.2.3`, год Revit и описание изменений.
Загрузка создаёт черновик. Кнопка «Опубликовать» открывает скачивание; публикацию
можно снять. Повторная загрузка той же версии для того же года Revit запрещена.
На странице скачивания показывается последняя опубликованная версия для каждого
года Revit. Установка и обновление выполняются вручную.

Сборки Revit хранятся отдельно в подкаталоге `.revit` каталога `DesktopReleases:Path`,
в существующем Docker volume. Они не участвуют в проверках обновлений Desktop.
