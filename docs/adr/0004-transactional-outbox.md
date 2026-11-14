# ADR-0004: Transactional outbox and idempotent inbox

Status: schema/write path implemented; dispatcher/adapters planned.

Decision: persist integration intent in the same SQL transaction as the state change and audit event. A future worker claims pending messages with locking, retries with backoff and dead-letters exhausted work. Inbound messages deduplicate by source/message identity and retain payload provenance.

Consequences: no dual write between SQL and a broker in the use case. Delivery is at-least-once, so consumers must be idempotent. The current preview records outbox entries but does not dispatch them; no SMS/email/ABDM/PACS delivery is claimed. Reconciliation and lag monitoring are required before enabling adapters.
