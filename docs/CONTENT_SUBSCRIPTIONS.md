# Content subscriptions

Authenticated API: GET, PUT and DELETE /api/content/{id}/subscription.
User ID and email come from the authenticated token. PUT requires a profile email.
Repeated PUT/DELETE is safe.

Flow:
IContentHistoryStore.AddAsync -> PostgreSQL history -> ContentChangePublisher ->
Kafka content-changes -> KafkaContentNotificationConsumer ->
ContentSubscriptions.HandleAsync -> INotificationService -> SMTP.

NotificationService contains the Kafka consumer, hosted by the API process.
It subscribes to Kafka:Topic using Kafka:BootstrapServers and the stable
Kafka:NotificationGroupId (default revex-content-notifications).
It consumes the existing publisher's ContentHistoryEventDto JSON. No history
table polling, trigger or PostgreSQL notification queue is used.
AddAsync waits for history persistence and Kafka publication, not SMTP delivery.
The existing publisher logs Kafka publication failures; history remains saved,
but those failed publications are not retried automatically.

Offsets are manually committed after every subscriber has been handled.
On SMTP or database failure the same Kafka event is retried after five seconds.
Successful per-user deliveries are recorded in content_notification_deliveries
and skipped on replay. An SMTP success followed by a crash before recording it
can still cause duplicate mail (at-least-once delivery).
Malformed events are logged and remain unacknowledged for investigation;
they block their partition until corrected.
A new consumer group starts at the earliest retained event; an existing group
resumes its committed offset. Keep the same group ID across restarts and instances.

Subscriptions have a subscribed_at timestamp: events from before subscribing
are excluded even when Kafka replays its backlog. A user who unsubscribes before
an event is handled receives no email. Previously saved subscriptions get the
timestamp of the schema update.
Messages contain the event description, author and UTC timestamp.
Configure NotificationService SMTP settings for actual delivery.

Validation:
dotnet build Backend/RevEx/RevEx.sln
Set REVEX_TEST_POSTGRES and REVEX_TEST_KAFKA to reachable test services, then run:
powershell -File Backend/RevEx/tests/subscriptions-checks.ps1
The integration test publishes through the real history store to an isolated Kafka
topic and runs the real consumer against PostgreSQL, substituting only email sending.
It verifies subscriber selection, replay deduplication, unsubscribe, subscription
time, retry after SMTP failure, offset commit and malformed payload rejection.
Temporary topic and schema are deleted afterwards.

Related task: https://github.com/DenisFazlul/RevEx.RevitPlugin/issues/4
