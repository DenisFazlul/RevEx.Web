# Управление сборками десктопа

## Администратор

Для существующего Keycloak настройте audience `revex-api` у клиента `revex-frontend`
(см. `Keycloak/README.md`) и войдите заново. Новый realm-import уже содержит эту
настройку. Без неё API правильно отклоняет токен фронтенда с ответом 401.

1. Войдите во frontend на вкладке Admin под пользователем с ролью `administrator`.
2. Скачайте результат Actions `RevEx-Desktop-Release-<version>` из репозитория десктопа.
3. Распакуйте внешний ZIP GitHub: внутри находится ZIP-комплект релиза.
4. В блоке «Сборки десктопа» выберите этот комплект и нажмите «Загрузить и проверить».
5. Выберите загруженную версию и поддерживаемый диапазон клиентов.
6. Нажмите «Активировать». На Home появятся ссылки на установщики Windows и macOS.

Архив не более 1 ГБ, распакованное содержимое не более 2 ГБ. Все файлы находятся
в корне, без папок, символических ссылок и повторяющихся имён. В release.json:

```json
{
  "version": "1.0.5",
  "notes": "Описание изменений",
  "installers": {
    "win-x64": "RevEx.Desktop-win-x64-Setup.exe",
    "osx-x64": "RevEx.Desktop-osx-x64-Setup.pkg",
    "osx-arm64": "RevEx.Desktop-osx-arm64-Setup.pkg"
  }
}
```

Также нужны три полных .nupkg и три releases.<platform>.json. Backend проверяет
SHA256, размер, идентификатор RevEx.Desktop, версию, rid и канал в спецификации
каждого пакета. Опубликованный номер версии нельзя перезаписать.

Необязательное исправление: оставьте предыдущую минимальную версию и включите
новую в диапазон. Обязательное: поднимите минимум до новой версии. Максимум
ограничивает поддержку более новых клиентов. Диапазон должен быть протестирован.

Первый релиз активируйте **до** установки пользователями. Пустой backend отвечает
404, установленный клиент позволяет повторить запрос или изменить адрес сервера.
Совместимый клиент может отказаться от обновления; несовместимый должен обновиться
или выйти. Клиенты без встроенного Velopack один раз переустанавливаются вручную.

## Хранилище и доступ

Compose подключает постоянный volume `desktop-releases` к `/app/storage/desktop-releases`.
Вне Docker путь задаётся `DesktopReleases:Path`. Не размещайте его в wwwroot.
Текущая реализация рассчитана на **один процесс backend**, управляющий этим хранилищем;
для нескольких реплик нужен распределённый lock и согласованное хранилище метаданных.

Загрузка, список и активация защищены ролью administrator. Сведения об активной
сборке и её файлы читаются без входа, чтобы обновление работало до авторизации.
Доступ к backend ограничивается сетью заказчика; в production используйте HTTPS.
Неактивные релизы и служебные record.json не выдаются через публичное API.

Подпись Windows и Apple Developer ID/notarization ещё требуют сертификатов владельца.
Эти credentials не заменяются тестовыми. На macOS для offline-поставки нужно также
прикрепить notarization ticket и проверить установку с политиками заказчика.

API:

- POST /api/desktop-releases — multipart/form-data, поле file.
- POST /api/desktop-releases/{version}/activate — version, minimumDesktopVersion, maximumDesktopVersion.
- GET /api/desktop-releases — административный список.
- GET /api/desktop-release — текущая политика и ссылки.
- GET /api/desktop-releases/{version}/files/{fileName} — потоковое скачивание с Range.

Текущий релиз переключается атомарно. При смене релиза во время скачивания клиент
может получить отказ и должен повторить стартовую проверку. Старые архивы сохраняются:
для отката выберите прежнюю загруженную версию и активируйте её, предварительно
проверив возможность downgrade и совместимость настроек.

## Проверки

```bash
cd Backend/RevEx
dotnet build RevEx.sln --disable-build-servers -m:1
dotnet run --project tests/DesktopReleaseChecks/DesktopReleaseChecks.csproj
```

Проверки покрывают staged/active, перезапуск хранилища, запрет перезаписи версии,
ZIP traversal, лишние файлы, контрольные суммы, каналы, лимиты, диапазон и порчу
после загрузки. Fixture из проверок не является настоящим установщиком и не должна
активироваться для пользователей.

Проверка настоящего ZIP из CI без публикации в рабочем хранилище:

```bash
dotnet run --project tests/DesktopReleaseChecks/DesktopReleaseChecks.csproj -- --validate-archive /absolute/path/RevEx-Desktop-Release-1.0.4.zip
```

Проверка создаёт собственную временную папку, проверяет загрузку/активацию/выдачу
трёх установщиков и удаляет эту папку после завершения.

Проверка реального HTTP API и ролей Keycloak (нужны запущенный локальный Compose,
Node.js 22+ и `.env` с паролями локальных пользователей). Выполняйте только на
одноразовом контейнере ниже, а не на рабочем API:

```bash
cd WEB
docker compose run -d --no-deps -p 18080:8080 -e DesktopReleases__Path=/tmp/revex-release-http-checks --name revex-release-http-checks revex-api
cd Backend/RevEx
dotnet run --project tests/DesktopReleaseChecks/DesktopReleaseChecks.csproj -- --write-fixture /private/tmp/revex-release-fixture.zip
node tests/DesktopReleaseChecks/http-checks.mjs
docker rm -f revex-release-http-checks
```

Проверяются 401/403 для неподходящих ролей, загрузка/активация, публичные ссылки,
Range, приватность служебных файлов и блокировка несовместимого клиента. Скрипт
использует обычный Authorization Code + PKCE, не меняет настройки Keycloak и не
выводит пароли или токены. Рабочий volume релизов не изменяется.

## Откат реализации

Исходное состояние сохранено baseline-коммитами в каждом репозитории. Реализация
разделена на backend, frontend/Compose, desktop и упаковку. Для отмены используйте
`git revert <commit>` в соответствующем Git-корне, в обратном порядке зависимостей.
После отката пересоберите сервисы. Не удаляйте volume с релизами: это отдельные данные,
они не исчезают при откате исходников.
