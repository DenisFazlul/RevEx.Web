# Запуск и эксплуатация

## Требования и адреса

Docker Desktop с Linux containers и Docker Compose v2.
Команды ниже выполняются из корня `RevEx.Web`.

| Сервис | Адрес хоста | Внутри Compose |
| --- | --- | --- |
| Frontend | http://localhost:3000 | revex-frontend:80 |
| API | http://localhost:8082 | revex-api:8080 |
| Keycloak | http://localhost:8081 | keycloak:8080 |
| MongoDB | localhost:27017 | mongodb:27017 |
| PostgreSQL | localhost:5433 | history-postgres:5432 |

Порт API 8082 выбран из-за другого сервиса Caddy на 8080 этой машины.
Не останавливайте чужой сервис для запуска RevEx. Если меняете порт, синхронизируйте
Compose, Desktop `AppSettings:ApiPath` и frontend development proxy/отображаемый адрес.
Контейнерный адрес `revex-api:8080` остаётся прежним.

## Первый запуск

```powershell
Copy-Item .env.example .env
# Отредактируйте .env перед запуском.
docker compose config --quiet
docker compose up -d --build
docker compose ps
docker compose logs --tail=100 keycloak revex-api
```

Не перезаписывайте существующий `.env` при обычном повторном запуске.
Первые скачивания образов и импорт realm требуют времени.
API зависит от healthy MongoDB/PostgreSQL; готовность Keycloak проверяйте отдельно:

```powershell
Invoke-RestMethod http://localhost:8081/realms/revex/.well-known/openid-configuration
Invoke-WebRequest http://localhost:3000/
Invoke-WebRequest http://localhost:8082/swagger/v1/swagger.json
```

API каталога требует bearer-токен. Проверка только HTML/Swagger не заменяет
вход и успешный `GET /api/categories`.

## Переменные .env

| Переменная | Назначение |
| --- | --- |
| `KEYCLOAK_ADMIN_USERNAME`, `KEYCLOAK_ADMIN_PASSWORD` | Bootstrap-администратор realm master, консоль Keycloak. |
| `REVEX_ADMIN_PASSWORD` | Пользователь admin realm revex. |
| `REVEX_USER_PASSWORD` | Пользователь user realm revex. |
| `REVEX_MODERATOR_PASSWORD` | Пользователь moder realm revex. |
| `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD` | База истории и учётная запись PostgreSQL. |

`.env` исключён из Git. Для этой локальной среды по запросу владельца настроены
`user/user`, `admin/admin` в revex и `admin/admin` для консоли master.
Это локальные тестовые значения; `.env.example` намеренно не содержит готовых паролей.
Пароль PostgreSQL и пароль moder в документацию не копируются.

**Изменение .env не меняет пароли существующих пользователей.** Realm импортируется
с `IGNORE_EXISTING`. Для работающего Keycloak меняйте пароль через его консоль/Admin API,
а затем согласуйте `.env` для последующего создания контейнера.
Bootstrap admin и admin приложения — разные аккаунты в разных realm.

## Сохранность данных

Volumes `mongodb-data`, `content-history-data`, `revex-files`, `desktop-releases`
сохраняют данные между обычными перезапусками. Keycloak в текущем Compose не имеет
постоянного volume: пересоздание его контейнера заново импортирует пользователей,
теряет локальные изменения realm и завершает существующие сеансы.

```powershell
docker compose stop
docker compose start
docker compose logs --tail=100 revex-api
```

`docker compose down` удаляет контейнеры и сеть; `down -v` дополнительно удаляет
данные volumes. Не используйте `down -v` для обычного перезапуска.
Перед переносом сохраняйте согласованные копии MongoDB, PostgreSQL и обоих файловых
volumes, а также `.env`, realm и тему. Восстановление проверяйте в отдельном окружении.

## Диагностика

| Симптом | Проверка |
| --- | --- |
| docker не найден | Проверьте PATH; на этой машине CLI находится в `%LOCALAPPDATA%\Programs\DockerDesktop\resources\bin`. В PATH нужна вся папка, включая credential helper. |
| 404 от Caddy | Запрос ушёл на чужой 8080; используйте API 8082. |
| 502 в nginx после пересоздания API | Убедитесь, что API готов; перезапустите `docker compose restart revex-frontend` для обновления адреса upstream. |
| invalid_user_credentials | Проверьте realm, логин и фактический пароль; правка .env сама его не меняет. |
| 401 | Проверьте issuer, audience, срок токена и доступ API к metadata Keycloak. |
| 403 | Пользователь вошёл, но не имеет роли для операции. |
| 409 desktop_version_unsupported | Клиент передал версию, которую политика релизов не поддерживает. |
| API не стартует | Проверьте MongoDB, строку PostgreSQL, health checks и логи инициализации. |
| Нет ссылки скачивания desktop | Сначала загрузите и опубликуйте релиз подходящей платформы. |

На 20.10.2026 проверены пять запущенных сервисов, вход admin в веб,
OIDC-вход user/admin и чтение категорий с HTTP 200, главное окно Desktop с каталогом.
