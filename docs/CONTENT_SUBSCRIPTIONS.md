# Content subscriptions

Authenticated API: GET, PUT and DELETE /api/content/{id}/subscription.
User ID and email come from the authenticated token. PUT requires a profile email.
Repeated PUT/DELETE is safe.

Flow:
IContentHistoryStore.AddAsync -> PostgreSQL history -> ContentChangePublisher ->
Kafka content-changes -> KafkaContentNotificationConsumer ->
ContentChangeHandler.HandleAsync -> INotificationService -> SMTP.

ContentSubscriptions implements IContentSubscriptions and only reads or changes
subscriptions. ContentChangeHandler implements IContentChangeHandler and handles
delivery to subscribers. They have separate dependency injection registrations.
Application interfaces live in `RevEx.Application/Notifications` and
`RevEx.Application/Subscriptions`; PostgreSQL/Kafka/SMTP implementations live in
`RevEx.Infrastructure/Notifications`.
AddNotificationService registers SMTP, subscriptions, the handler and Kafka consumer.
The API hosts the consumer and calls this registration method; there is no separate
NotificationService project. The NotificationService configuration section is preserved.
It subscribes to Kafka:Topic using Kafka:BootstrapServers and the stable
Kafka:NotificationGroupId (default revex-content-notifications).
It consumes the existing publisher's ContentHistoryEventDto JSON. No history
table polling, trigger or PostgreSQL notification queue is used.
AddAsync waits for history persistence and the initial Kafka publication, not SMTP delivery.
On a confirmed transport error the producer writes a PostgreSQL fallback outbox entry;
the hosted dispatcher retries it every 30 seconds. History and fallback outbox are not
atomic; the remaining failure window is documented in CONTENT-CHANGE-EVENTS.md.

Offsets are manually committed after every subscriber has been handled.
On SMTP or database failure the same Kafka event is retried after five seconds.
Successful per-user deliveries are recorded in content_notification_deliveries
and skipped on replay. An SMTP success followed by a crash before recording it
can still cause duplicate mail (at-least-once delivery).
Pending recipients are selected by one SQL query. SMTP send and the delivery marker
are intentionally sequential per recipient: persisting every successful recipient avoids
resending it when a later recipient fails. This is not an EF lazy-loading/N+1 read.
Malformed events are logged and remain unacknowledged for investigation;
they block their partition until corrected.
A new consumer group starts at the earliest retained event; an existing group
resumes its committed offset. Keep the same group ID across restarts and instances.

Subscriptions have a subscribed_at timestamp: events from before subscribing
are excluded even when Kafka replays its backlog. A user who unsubscribes before
an event is handled receives no email.
Messages contain the event description, author and UTC timestamp.
Configure NotificationService SMTP settings for actual delivery.

Validation:

```powershell
dotnet build Backend/RevEx/RevEx.sln
$env:REVEX_TEST_POSTGRES = 'Host=...;Database=...;Username=...;Password=...'
$env:REVEX_TEST_KAFKA = 'host:9092'
powershell -File Backend/RevEx/tests/subscriptions-checks.ps1
```

The integration test publishes through the real history store to an isolated Kafka
topic and runs the real consumer against PostgreSQL, substituting only email sending.
It verifies subscriber selection, replay deduplication, unsubscribe, subscription
time, retry after SMTP failure, offset commit and malformed payload rejection.
Temporary topic and schema are deleted afterwards.

Related task: https://github.com/DenisFazlul/RevEx.RevitPlugin/issues/4
