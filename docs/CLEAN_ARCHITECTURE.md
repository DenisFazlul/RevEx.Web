# Границы бэкенда

API отвечает за HTTP, авторизацию, multipart и сборку зависимостей. Application/Content/ContentService выполняет создание, изменение, удаление контента и управление превью, проверяет связанные сущности и записывает историю. HTTP-ошибки преобразуются в API-фильтре; Application не зависит от ASP.NET Core.

Application/Abstractions содержит порты хранения и ICurrentUser. IContentSubscriptions находится в Application/Subscriptions, PostgreSQL-реализация — в Infrastructure. API получает автора истории через HttpCurrentUser, без вызовов других контроллеров.

Domain содержит сущности, Contracts — общие DTO. Сборки независимы; пространства имён DTO сохранены. Потребители явно ссылаются на обе библиотеки. Для пакетной сборки требуются новые пакеты Domain 1.1.0 и Contracts 1.0.0.

IContentQueries реализован MongoContentQueries: фильтрация по категориям, всем выбранным тегам и части названия выполняется в БД. Offset и Limit необязательны; сортировка по Id обеспечивает устойчивый порядок. Без Limit результат остаётся неограниченным для совместимости.

Проверки: dotnet build Backend/RevEx/RevEx.sln; dotnet run --project Backend/RevEx/tests/ApplicationChecks; dotnet run --project ../RevEx.Domain/tests/ContractChecks -f net8.0.

Сценарии каталога, версий, файлов, истории и подписок перенесены в Application; слой Application не требует MediatR или CQRS. Запись MongoDB, файлов и истории PostgreSQL не является одной атомарной транзакцией; выделение слоёв само по себе не меняет эту гарантию.

## Проверка 30 октября 2026

Собраны Backend, Desktop, Connecter, MCP, RevitPlugin, TestHub и DomainIntegrationHost. Проверены JSON-контракты на net8.0 и net47, прикладные сценарии, HTTP с отдельной тестовой MongoDB, Desktop UI в Release, обмен задачами Desktop/коннектор, обновление Desktop, релизы, PostgreSQL tracking и Kafka subscriptions с имитацией почтовой доставки. Тестовая MongoDB удаляется после проверки; PostgreSQL/Kafka-проверки используют собственные временные схемы и топики.

Новые пакеты Domain и Contracts собраны локально, API успешно собран в пакетном режиме из локального источника. Публикация пакетов во внешний реестр не выполнялась.

Обновлённый API запущен в существующем Compose-окружении. Swagger, frontend и конфигурация Keycloak отвечают 200; защищённый API без авторизации отвечает 401. Сборка исходников внутри Docker задерживалась на восстановлении NuGet; для проверки запуска использован образ с результатом локального dotnet publish. Обычный Dockerfile сохранён.

Проверка Desktop UI в Debug останавливается на существующем подключении DevTools к headless-приложению; Release-проверка проходит. Загрузка семейства в настоящий Revit и полный вход через браузер не проверялись.

## Организация Infrastructure

Обе базы следуют одному правилу:

- `Persistence/` — MongoDbContext, PostgresDbContext, фабрики, параметры подключения, модель EF и миграции. Служебные модели хранения остаются здесь.
- `Repositories/` — реализации доступа к данным: отдельные Mongo-репозитории сущностей, MongoContentQueries, PostgresCommentStore, PostgresContentHistoryStore, PostgresDesktopUpdateTrackingStore.
- `Messaging/` — публикация событий и очередь Kafka, включая ContentChangePublisher и KafkaOutboxStore.
- `Notifications/` — подписки и доставка уведомлений как отдельная функциональность.

Суффикс Store сохранён для специализированных интерфейсов Application: это не требует отдельного слоя или дублирования generic repository поверх DbContext. API зависит от интерфейсов Application; контексты не передаются контроллерам. Размещение по функциональности (Messaging, Notifications) допустимо наряду с общей папкой Repositories; правило одинаково для обеих БД.

Основание: [Microsoft: архитектура веб-приложений](https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/common-web-application-architectures) и [реализация слоя хранения с EF Core](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/infrastructure-persistence-layer-implementation-entity-framework-core). Конкретные имена папок — соглашение проекта, а не требование EF.


## Уточнение границ после ревью

Каталог, версии, файлы, история и подписки делегируют сценарии Application. HTTP, авторизация и преобразование multipart остаются в API. Каскадное удаление контента, версий и очистка ссылок на теги выполняются прикладными сервисами. Каждая сущность имеет самостоятельный интерфейс ICategoryRepository, IContentRepository, IContentStatusRepository, ITagRepository, ITagGroupRepository, IContentVersionRepository или IContentFileRepository. Пять общих CRUD-сигнатур объявлены один раз в IRepository<TEntity>; интерфейсы конкретных репозиториев наследуют этот контракт. Специфичные методы объявляются только в соответствующем интерфейсе.

Mongo-реализации наследуются от абстрактного MongoRepository<TEntity>, реализующего IRepository<TEntity>. Базовый класс содержит виртуальные CRUD-методы: чтение документов без связей, выделение Id, временные метки и немедленное сохранение. Контекст и общий метод сохранения доступны наследникам через protected Context и SaveAsync. Назначение Id передаётся в конструктор делегатом, без изменения Domain или проверок типа сущности. MongoCategoryRepository, MongoContentFileRepository, MongoContentStatusRepository и MongoTagGroupRepository используют унаследованный CRUD без обёрток. MongoContentRepository, MongoTagRepository и MongoContentVersionRepository переопределяют GetAllAsync и GetByIdAsync, вызывают базовое чтение и загружают необходимые связи; списки используют пакетную загрузку. Остальные CRUD-операции наследуются. MongoEntityStore удалён; отдельного HydrateAsync нет. Базовый класс не содержит каскадных или прикладных правил и отдельно в DI не регистрируется. PostgreSQL-хранилища остаются самостоятельными реализациями своих интерфейсов.

Каждая мутация репозитория сохраняется немедленно. Пустой UnitOfWork удалён. Несколько мутаций, файлы и история не образуют общей транзакции; частичное выполнение остаётся возможным. Схема Kafka с резервной записью outbox после ошибки публикации оставлена по согласованному допущению.

ContentNotificationService в Application выбирает порядок действий через INotificationDeliveryStore и INotificationService. SQL и отметки доставки находятся в PostgresNotificationDeliveryStore, SMTP — в SmtpNotificationService. Правило доставки: после успешного SMTP фиксируется отметка; отмеченные доставки пропускаются, неуспешные повторяются. При остановке между SMTP и отметкой возможно повторное письмо: SMTP не обеспечивает атомарную транзакцию с PostgreSQL. Это доставка с возможными повторами, а не гарантия exactly-once.

Проверка после исправлений: backend собран без предупреждений и ошибок; ApplicationChecks, расширенные ApiChecks с реальной MongoDB и DesktopReleaseChecks прошли. ContentSubscriptionChecks прошёл с PostgreSQL и Kafka в существующей Docker-сети, с отдельными временными схемой и топиком: выбор подписчиков, повтор события, отписка, поздняя подписка, повтор после SMTP-ошибки, Kafka offsets и некорректное событие. Проверены отсутствие UnitOfWork, прямого доступа контроллеров к репозиториям и зависимостей Application от ASP.NET/Infrastructure.
