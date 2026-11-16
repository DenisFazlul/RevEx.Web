# События изменения контента

После сохранения `ContentHistoryEvent` в PostgreSQL backend публикует его DTO в Kafka.
Producer использует `acks=all`, idempotent transport и timeout 10 секунд. При успешной
публикации HTTP-сценарий продолжается без записи в outbox.

Если Kafka возвращает `ProduceException`, сообщение сохраняется через EF Core в
`kafka_outbox`. При отмене исходного запроса producer также пытается сохранить fallback
отдельным десятисекундным токеном и затем повторно выбрасывает cancellation. Hosted service
`KafkaOutboxDispatcher` выбирает по одному готовому сообщению через
`FOR UPDATE SKIP LOCKED`; при успехе удаляет запись, при ошибке увеличивает `attempts`,
сохраняет `last_error` и назначает повтор через 30 секунд.

История и fallback outbox не записываются одной транзакцией: сначала фиксируется история,
затем выполняется Kafka send, а outbox создаётся только после известного transport failure.
Аварийное завершение процесса между этими шагами всё ещё может потерять уведомление.
Message ID стабилен при повторной отправке из outbox, а consumer дополнительно хранит
идемпотентные отметки доставки по `(event_id, user_id)`.

## Конфигурация и запуск

Корневой `.env` задаёт `KAFKA_BOOTSTRAP_SERVERS` (по умолчанию `kafka:9092` внутри
Compose), `KAFKA_TOPIC` (`content-changes`) и `KAFKA_RETENTION_MS`
(`864000000`, десять дней). Standalone API использует `Kafka__BootstrapServers`,
`Kafka__Topic` и `Kafka__NotificationGroupId`. Compose не публикует Kafka на host.

`docker compose up -d --build` запускает single-node plaintext broker. Volume `kafka-data`
сохраняет журнал. One-shot service `kafka-init` создаёт topic и обновляет retention;
API стартует после его успешного завершения. После изменения retention выполните:

```powershell
docker compose run --rm kafka-init
```

Topic использует `cleanup.policy=delete`, unlimited `retention.bytes`, сегменты по одному часу.
Физическое удаление просроченных сегментов выполняется Kafka асинхронно.

## Контракт

Сообщение — camelCase JSON: `id`, `contentId`, `occurredAt`, `description`, `author`.
Kafka key — строковый content ID. Consumer group вручную commit’ит offset только после
успешной обработки всех получателей; детали — в [CONTENT_SUBSCRIPTIONS.md](CONTENT_SUBSCRIPTIONS.md).
