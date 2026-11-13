# Careflow HIS — System architecture

## 1. Scope and delivery status

Careflow is a proposed multi-hospital HIS + EMR + LIS + RIS platform. This repository begins at an empty README. The first delivery establishes the architecture, SQL Server schema, .NET solution, and a usable Phase 1 clinical workspace. **It is not a certified or deployment-ready hospital system.** Real deployment requires the release gates in ROADMAP.md, clinical validation, infrastructure integration tests, and a hospital-specific security/privacy review.

The browser preview is an explicitly labeled, synthetic-data workspace. Browser-local persistence demonstrates workflows; it is not a secure medical-record store. Production browser code must use the authenticated API, never the demo store. No actual ABDM, DICOM, payment, messaging, analyzer, or video connection is claimed.

## 2. Architectural decisions

- **Modular monolith first:** independently owned domain/application modules inside a deployable ASP.NET Core API. Split services only for demonstrated scaling, availability, or organizational needs. Distributed transactions are not the default for clinical workflows.
- **Clean boundaries:** Domain has no infrastructure dependencies; Application owns use cases, authorization requirements, DTOs and ports; Infrastructure implements SQL Server, durable outbox and adapters; Api is the composition root and HTTP boundary.
- **Frontend:** React + TypeScript, responsive desktop-first shell, role-aware navigation, accessible controls and a shared design system. Server authorization is authoritative; hiding a menu is not a security boundary.
- **Data:** Microsoft SQL Server, normalized OLTP schemas, integer internal identifiers, external GUID identifiers, decimal money, UTC instants, hospital/branch ownership, optimistic concurrency and explicit reference constraints.
- **Identity:** ASP.NET Core Identity-compatible user model with a unique login, locked/disabled accounts, hashed passwords, MFA enrollment boundary, revocable server sessions and short-lived authenticated context. The initial SQL adapter implements password hashing and opaque sessions; an enterprise OIDC/IdentityServer deployment is a release integration, not an implied finished feature.
- **Authorization:** explicit permission grants through roles, scoped to hospital and optionally branch; clinical relationship checks for patient access; privileged operations require a reason and elevated permission. No client-supplied role is trusted by the production API.
- **Audit:** append-only security/clinical/financial audit events in the same transaction as the change. Restrict application principal from updating/deleting audit rows. Audit storage immutability/WORM export is an infrastructure control, not achieved merely by a boolean column.
- **Integration:** transactional outbox → dispatch workers → idempotent adapters; inbox/deduplication on inbound messages. HL7 v2 / FHIR R4 mappings, DICOMweb/PACS, ABDM, notifications and payments are separate tested adapters.
- **Configuration:** hospital/branch module flags, policies, numbering rules, schedules, prices, templates and approval thresholds are data. Entitlements never override patient-safety restrictions.
- **Safety:** signed prescriptions/reports are immutable; corrections create a new version linked to the original. Critical-result acknowledgment, allergy checks, consent and traceability are explicit states/events.

## 3. Logical system

```text
Staff web / patient portal / future mobile clients
                 | HTTPS, same-origin BFF/API
       Edge: TLS, WAF, rate limits, request limits
                 |
 ASP.NET Core API: authentication, scope, RBAC, validation,
                  correlation IDs, problem responses, audit
                 |
 Application use cases + transaction/unit-of-work boundary
                 |
 Domain invariants + module contracts + authorization policies
                 |
 SQL Server repositories | durable outbox | object storage
                 |                 |
 SQL Server OLTP + audit       integration workers
                                   |
        +-------------+------------+-------------+
        |             |            |             |
    ABDM / FHIR    HL7 / LIS    DICOMweb/PACS  payment / messaging
```

Use a single trusted origin in the preview. Production uses HTTPS and a same-origin reverse proxy; never expose SQL Server to a browser. Private blob storage holds photographs, identity documents, signed PDFs and clinical attachments, with short-lived authorization-checked downloads. DICOM pixels remain in PACS/VNA, not ordinary relational rows.

## 4. Solution organization

```text
src/Careflow.Domain/          entities, value objects, domain rules
src/Careflow.Application/     use-case contracts, DTOs, authorization, ports
src/Careflow.Infrastructure/  SQL Server, identity, audit, outbox/adapters
src/Careflow.Api/             REST endpoints, composition, security middleware
src/Careflow.Web/             responsive staff workspace and demo adapter
 tests/                       domain, application, API and browser tests
 database/                    versioned SQL migrations and deployment notes
 docs/                        architecture, module contracts, ADRs, runbooks
```

Modules: Identity, Organization, Masters, Patient, Scheduling, Encounter, PharmacyCatalog, Billing, Admission, Nursing, Emergency, Theatre, Laboratory, Radiology, BloodBank, Inventory, Quality, Insurance, Incentives, Telemedicine, Portal, Reporting. A module contract exposes commands/queries and integration events, not another module's tables.

## 5. Tenancy and ownership

Every tenant-owned aggregate includes HospitalId. BranchId identifies the operating location, not a security boundary by itself. Encounter, order, specimen, report, invoice and audit records carry both ownership identifiers. Resolve hospital/branch membership from the authenticated session. Reject foreign resource IDs before loading sensitive details. Cross-hospital patient linking requires explicit consent/authorization and a governed master patient index; do not silently share an entire chart.

UHID is unique within a hospital; accession, invoice and token sequences are unique within their configured scope. Use a durable sequence-allocation transaction, not MAX(number)+1. UHID is not a national identifier. ABHA is a separate, verified linkage.

## 6. Data consistency and concurrency

- One SQL transaction for an aggregate change, audit event and outbox message.
- RowVersion checks on mutable clinical/financial records; conflicts return 409, never last-write-wins.
- Unique constraints for active memberships, UHID, appointment slots, sample barcodes and payment idempotency keys.
- Billing uses integer minor-unit amounts or decimal(19,4), never binary floating point. Initial Phase 1 uses INR decimal(19,4), explicit tax/discount and payment allocation.
- State transitions are explicit. A payment cannot make a signed invoice mutable; issue a credit note/refund instead.
- Soft deletion applies only to administrative records. Signed medical/financial records are retained and corrected/voided by audited events according to an approved retention policy.
- Read replicas may power dashboards; clinical decisions and authorization use the primary or a consistency-guaranteed read.

## 7. Security and privacy baseline

TLS; secure HttpOnly SameSite cookies; CSRF/origin checks on cookie-authenticated writes; parameterized SQL; least-privilege database identities; encrypted disks/backups; secret-manager integration; password policy and lockout; MFA for privileged/remote access; session revocation; login history; no PHI in routine logs; access-purpose/break-glass audit; rate limiting; upload type/size validation and malware scanning; signed URLs with short expiry; tested restore and retention policies. Indian DPDP obligations, clinical retention, accreditation and ABDM conformance must be validated for the deploying organization. A feature checklist is not legal certification.

## 8. Operations and scale

Stateless API replicas behind a load balancer; SQL Server appropriate HA tier; durable queues/workers; cache only non-authoritative reference data; OpenTelemetry traces/metrics/logs with redaction; liveness vs readiness endpoints; dashboards for latency, queue lag, failed integrations, deadlock rate, login failures and critical-alert delivery. Partition large append-only audit/result tables by time after measurement. Index by actual tenant/query patterns. Pool connections, paginate every collection, cap page sizes, avoid N+1 requests and use cancellation tokens. Define and test RPO/RTO with the hospital. Start with one deployable unit; extract imaging/integration/analytics workloads first when justified.

## 9. Interoperability contracts

| Domain             | Planned standard / adapter                                                                       | Safety boundary                                                      |
| ------------------ | ------------------------------------------------------------------------------------------------ | -------------------------------------------------------------------- |
| Patient / clinical | FHIR R4 Patient, Encounter, Observation, AllergyIntolerance, DiagnosticReport, MedicationRequest | Versioned mappings, terminology validation, provenance               |
| Lab / ADT          | HL7 v2 ORU/ORM/ADT; analyzer ASTM/LIS2-A2 where supported                                        | Acknowledgment, retries, specimen identity, instrument QC            |
| Imaging            | DICOM MWL, MPPS, storage; DICOMweb QIDO-RS/WADO-RS/STOW-RS                                       | accession linkage, patient reconciliation, controlled viewer launch  |
| India              | ABHA verification, consent artefacts, health-information exchange                                | verified consent, purpose/expiry, revocation, sandbox certification  |
| Notifications      | SMS/email/WhatsApp provider adapter                                                              | consent, opt-out, minimum necessary content, delivery receipts       |
| Payments           | gateway adapter with signed webhooks                                                             | server-verified amount, idempotency, reconciliation, no card storage |
| Terminology        | ICD, SNOMED CT, LOINC, UCUM, RxNorm/local formulary as licensed                                  | licensing, provenance, effective dates; do not invent coded meanings |

## 10. User roles and navigation

See MODULE_SPECIFICATIONS.md for the permission matrix and per-module contracts. Default staff navigation:

1. Overview
2. Patients (registry, chart, consents, merge review)
3. Appointments (calendar, schedules, live queue)
4. Consultations (doctor worklist, EMR, prescriptions, orders)
5. Inpatient (ADT, bed board, nursing, discharge)
6. Emergency (triage, observation, disposition)
7. Diagnostics (LIS, RIS, PACS links, critical alerts)
8. Theatre / Blood bank
9. Pharmacy / Stores / CSSD
10. Billing / Insurance / Incentives
11. Quality / Reports
12. Administration (organization, users, roles, masters, module settings, audit)

Patient navigation is a separate least-privilege portal: health record, appointments, prescriptions, reports, bills/payments, consent, telemedicine. Navigation is filtered by enabled module + permission + scope; every endpoint repeats the checks.

## 11. Important ADRs

- ADR-0001: modular monolith over premature microservices.
- ADR-0002: tenant-scoped SQL constraints and server-derived tenant context.
- ADR-0003: immutable signed records and append-only corrections.
- ADR-0004: transactional outbox rather than dual writes.
- ADR-0005: synthetic browser demo is isolated from production persistence.
- ADR-0006: standards-based adapters; integration-ready is not integrated.

Full decision records and acceptance criteria accompany implementation increments in docs/adr/.
