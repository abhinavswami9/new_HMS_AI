# ADR-0005: Explicit synthetic adapter, shared application contracts

Status: accepted.

Decision: the live development preview uses the real HTTP API and application services with an in-memory synthetic adapter. Demo mode is explicit, refuses Production environment, seeds only synthetic records and exposes a role selector. The browser does not persist medical records. SQL Server mode uses the same use-case contracts with a parameterized ADO.NET adapter.

Consequences: workflow tests can run without SQL Server, but they do not prove SQL correctness, durability or production security. Demo credentials are public development fixtures and are never inserted by migrations. Restart resets data. Real deployment requires SQL integration tests and removal of demo configuration.
