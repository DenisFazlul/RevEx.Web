# Content change events

Every new content history entry is also queued in PostgreSQL in the same SQL statement.
The API background worker publishes pending entries to Kafka and removes each queued
entry only after Kafka acknowledges it. Broker outages do not discard pending events.
Existing history is not backfilled. Database history retention is unchanged.

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

Messages use the existing content history DTO serialized as camelCase JSON:
`id`, `contentId`, `occurredAt`, `description`, `author`. The key is the content ID.
Delivery is at least once: a crash between Kafka acknowledgement and PostgreSQL
commit can cause a duplicate. Consumers must deduplicate by history event `id`.
Producer idempotence handles producer retries, but does not eliminate that crash window.
Pending rows remain until sent; monitor API delivery warnings during long outages.

## Manual delivery and recovery check

1. Start Compose and open a consumer:
   `docker compose exec kafka /opt/kafka/bin/kafka-console-consumer.sh --bootstrap-server kafka:9092 --topic content-changes --from-beginning`
2. Make an authenticated content edit. Compare the JSON event with its history entry.
3. Stop Kafka (`docker compose stop kafka`), make another edit and confirm history
   still succeeds. Restart Kafka (`docker compose start kafka`) and confirm delivery.
4. Restart the API while Kafka is unavailable and verify the pending event is delivered
   after recovery. Pending entries can be inspected in `content_change_outbox`.
5. Inspect the effective topic settings:
   `docker compose exec kafka /opt/kafka/bin/kafka-configs.sh --bootstrap-server kafka:9092 --entity-type topics --entity-name content-changes --describe`
   Confirm `retention.ms=864000000` and `cleanup.policy=delete`.
## Automated local integration check

With the Compose stack running, execute:

```powershell
node Backend/RevEx/tests/ContentChangeChecks/http-checks.mjs
```

The check logs in through Keycloak using authorization code + PKCE and the admin
credentials from the root `.env`; it does not enable password grants or bypass auth.
It creates its own content, checks creation/update event payloads against database
history, stops Kafka, verifies durable queueing across an API restart, restores Kafka,
and checks delivery and event persistence across a broker restart. It then deletes
the test content; audit entries remain intentionally. Run when brief interruptions
of the local Kafka and API are acceptable. It also checks effective topic retention
and that anonymous API requests remain unauthorized. It verifies the configured
ten-day policy, without waiting ten days for segment deletion.
