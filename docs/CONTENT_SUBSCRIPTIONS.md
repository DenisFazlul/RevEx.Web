# Content subscriptions

Authenticated API: GET, PUT and DELETE /api/content/{id}/subscription.
The user identity and email come from the authenticated token, never the request body.
PUT requires a profile email. Repeating PUT/DELETE is safe.

At startup the API creates PostgreSQL subscription and notification queue tables
and a trigger on content_history_events. Every new history event atomically queues
one notification per subscriber. Existing history is not replayed.
The background service checks the queue every 30 seconds and uses NotificationService
SMTP configuration. Failed deliveries remain pending. Multiple API instances lock
queue rows to avoid simultaneous delivery. Delivery is at least once: a process crash
after SMTP accepts a message and before database commit can cause a duplicate.
Unsubscribing stops future history events; already queued messages remain deliverable.
Stored email is refreshed when subscribing again.

Validation: dotnet build Backend/RevEx/RevEx.sln
PostgreSQL integration check (local Compose running):
powershell -File Backend/RevEx/tests/subscriptions-checks.ps1
The check rolls back schema and data. Real SMTP delivery requires configured credentials.

Related task: https://github.com/DenisFazlul/RevEx.RevitPlugin/issues/4
