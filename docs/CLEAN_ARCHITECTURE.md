# Границы бэкенда

API отвечает за HTTP, авторизацию, multipart и сборку зависимостей. Application/Content/ContentService выполняет создание, изменение, удаление контента и управление превью, проверяет связанные сущности и записывает историю. HTTP-ошибки преобразуются в API-фильтре; Application не зависит от ASP.NET Core.

Application/Abstractions содержит порты хранения и ICurrentUser. IContentSubscriptions находится в Application/Subscriptions, PostgreSQL-реализация — в Infrastructure. API получает автора истории через HttpCurrentUser, без вызовов других контроллеров.

Domain содержит сущности, Contracts — общие DTO. Сборки независимы; пространства имён DTO сохранены. Потребители явно ссылаются на обе библиотеки. Для пакетной сборки требуются новые пакеты Domain 1.1.0 и Contracts 1.0.0.

IContentQueries реализован MongoContentQueries: фильтрация по категориям, всем выбранным тегам и части названия выполняется в БД. Offset и Limit необязательны; сортировка по Id обеспечивает устойчивый порядок. Без Limit результат остаётся неограниченным для совместимости.

Проверки: dotnet build Backend/RevEx/RevEx.sln; dotnet run --project Backend/RevEx/tests/ApplicationChecks; dotnet run --project ../RevEx.Domain/tests/ContractChecks -f net8.0.

Остальные контроллеры пока сохраняют существующие CRUD-сценарии. Их перенос можно выполнять отдельно по областям; слой Application не требует MediatR или CQRS. Запись MongoDB, файлов и истории PostgreSQL не является одной атомарной транзакцией; выделение слоёв само по себе не меняет эту гарантию.

## Проверка 30 октября 2026

Собраны Backend, Desktop, Connecter, MCP, RevitPlugin, TestHub и DomainIntegrationHost. Проверены JSON-контракты на net8.0 и net47, прикладные сценарии, HTTP с отдельной тестовой MongoDB, Desktop UI в Release, обмен задачами Desktop/коннектор, обновление Desktop, релизы, PostgreSQL tracking и Kafka subscriptions с имитацией почтовой доставки. Тестовая MongoDB удаляется после проверки; PostgreSQL/Kafka-проверки используют собственные временные схемы и топики.

Новые пакеты Domain и Contracts собраны локально, API успешно собран в пакетном режиме из локального источника. Публикация пакетов во внешний реестр не выполнялась.

Обновлённый API запущен в существующем Compose-окружении. Swagger, frontend и конфигурация Keycloak отвечают 200; защищённый API без авторизации отвечает 401. Сборка исходников внутри Docker задерживалась на восстановлении NuGet; для проверки запуска использован образ с результатом локального dotnet publish. Обычный Dockerfile сохранён.

Проверка Desktop UI в Debug останавливается на существующем подключении DevTools к headless-приложению; Release-проверка проходит. Загрузка семейства в настоящий Revit и полный вход через браузер не проверялись.
