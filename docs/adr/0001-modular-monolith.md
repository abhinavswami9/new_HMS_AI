# ADR-0001: Modular monolith first

Status: accepted for the initial platform.

Context: clinical/financial workflows need atomic state changes and audit. Premature service decomposition introduces distributed transactions and operational overhead without a measured scaling need.

Decision: one ASP.NET Core deployable API with Domain/Application/Infrastructure boundaries and explicit module contracts. SQL transactions protect aggregate changes, audit and outbox. Extract integration/analytics/imaging workloads when availability or load justifies it.

Consequences: simpler consistency and local development; module discipline is enforced by project/review conventions. A shared deployment is a failure domain, so HA replicas, readiness checks and tested recovery remain mandatory. Revisit extraction using measured throughput, team ownership and isolation requirements.
