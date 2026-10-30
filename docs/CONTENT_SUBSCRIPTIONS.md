# Content subscriptions

Authenticated API: GET, PUT and DELETE /api/content/{id}/subscription.
The user identity and email come from the authenticated token, never the request body.
PUT requires a profile email. Repeating PUT/DELETE is safe.

IContentHistoryStore.InitializeAsync creates PostgreSQL history, subscription and queue
tables and removes the old notification trigger. IContentHistoryStore.AddAsync saves
the event and queues one notification per subscriber in a single atomic SQL statement.
Direct SQL history inserts do not create notifications. Existing history is not replayed.
The background service checks the queue every 30 seconds and uses NotificationService
SMTP configuration. Failed deliveries remain pending. Multiple API instances lock
queue rows to avoid simultaneous delivery. Delivery is at least once: a process crash
after SMTP accepts a message and before database commit can cause a duplicate.
Unsubscribing stops future history events; already queued messages remain deliverable.
Stored email is refreshed when subscribing again.

Validation: dotnet build Backend/RevEx/RevEx.sln
PostgreSQL integration check: set REVEX_TEST_POSTGRES to a test database connection string, then run:
powershell -File Backend/RevEx/tests/subscriptions-checks.ps1
The check uses a unique isolated schema and deletes it afterwards. It exercises the actual IContentHistoryStore implementation, migration from the trigger, subscriber selection, unsubscribe and atomic rollback. Kafka unavailability may produce expected logs. Real SMTP delivery requires configured credentials.

Related task: https://github.com/DenisFazlul/RevEx.RevitPlugin/issues/4
