# RevEx Web

ASP.NET Core API, React-интерфейс, Keycloak и инфраструктура Docker Compose для библиотеки контента RevEx.

Полная [документация проекта](docs/README.md) находится рядом с `docker-compose.yml`.

## Быстрый запуск

Скопируйте `.env.example` в `.env`, задайте все пароли и выполните из корня:

```powershell
docker compose config --quiet
docker compose up -d --build
docker compose ps
```

- Веб: <http://localhost:3000>
- API / Swagger: <http://localhost:8082/swagger>
- Keycloak: <http://localhost:8081>

В контейнерной сети API слушает порт 8080; наружу опубликован 8082.
При локальной разработке Vite учитывайте отдельную настройку proxy.

См. [запуск и диагностику](docs/OPERATIONS.md), [API](docs/API.md),
[модель данных](docs/ENTITIES.md) и [релизы desktop](docs/DESKTOP_RELEASES.md).


Общие сущности и DTO вынесены в отдельный репозиторий: [подключение Domain](docs/DOMAIN.md).
