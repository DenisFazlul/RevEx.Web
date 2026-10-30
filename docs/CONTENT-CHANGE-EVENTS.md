# Content change events

After saving a content history entry in PostgreSQL, the API publishes its DTO directly
to Kafka and waits for acknowledgement (delivery timeout: ten seconds).
Kafka errors are logged; the saved history and successful API operation are preserved.
There is no persistent queue, background dispatcher, backfill or later retry.
A broker outage or process crash can therefore lose notifications.
Existing legacy queue tables are no longer used and are left untouched.

## Configuration and startup

The root `.env` configures `KAFKA_BOOTSTRAP_SERVERS` (default `kafka:9092` inside
Compose), `KAFKA_TOPIC` (default `content-changes`) and `KAFKA_RETENTION_MS`
(default `864000000`, ten days). Standalone API configuration uses
`Kafka__BootstrapServers` and `Kafka__Topic`; standalone defaults are
`localhost:9092` and `content-changes`. Compose does not expose Kafka to the host.

Run `docker compose up -d --build`. The Kafka broker stores data in `kafka-data`.
The one-shot `kafka-init` service creates the topic and applies retention settings
also to an existing topic. The API waits for successful initialization.
After changing retention, rerun `docker compose run --rm kafka-init`.

The topic uses delete cleanup, unlimited size retention, and one-hour segments.
Kafka deletes expired segments asynchronously: physical deletion can lag the ten-day
window by a segment interval, a retention check (five minutes), and deletion delay.
This is a single-node development broker using plaintext inside the Compose network.

## Message contract

Messages are camelCase JSON: id, contentId, occurredAt, description, author.
The key is the content ID. Producer idempotence is enabled for Kafka transport retries.
Delivery is not atomic with PostgreSQL history writes.

## Automated local integration check

With Compose running, execute:

node Backend/RevEx/tests/ContentChangeChecks/http-checks.mjs

The check authenticates with Keycloak, creates and updates test content, compares Kafka
messages with history, and verifies persistence across a broker restart. It deletes its
content afterwards; audit history remains. Run when a Kafka restart is acceptable.
