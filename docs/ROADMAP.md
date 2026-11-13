# Delivery roadmap and production gates

## Increment strategy

Each increment: contract/schema → domain/application tests → API authorization/audit → UI workflow → integration/failure tests → clinical/finance review → release candidate. The workspace is a synthetic vertical slice, not a claim that all Phase 1/Phase 2 requirements are complete. Status is tracked here rather than inferred from navigation badges.

| Phase | Scope                                                                                       | Current delivery                                                                                                                                           |
| ----- | ------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1     | Identity/RBAC, setup, masters, patients/UHID, appointments, OPD/EMR, prescriptions, billing | Delivered initial vertical slice; see PHASE1_STATUS.md for partial/remaining enterprise gates                                                              |
| 2     | IPD, beds, nursing, pharmacy, LIS                                                           | Delivered initial vertical slice: ADT/bed board, nursing/eMAR, pharmacy stock/dispensing, LIS worklist; see PHASE2_STATUS.md                               |
| 3     | RIS/PACS, OT, emergency, blood bank                                                         | Delivered initial vertical slice: RIS workflow (no live PACS — viewer fails closed), emergency board, theatre scheduling, blood bank; see PHASE3_STATUS.md |
| 4     | Insurance/TPA, incentives, telemedicine, inventory/procurement                              | Configurable workflow design; no live payment/video service                                                                                                |
| 5     | Quality, reporting, portal, mobile, AI-ready                                                | Governance/read-model design; no autonomous clinical decisions                                                                                             |

## Phase 1 work packages

1. **Foundation:** solution boundaries, SQL migration, problem responses, health checks, synthetic UI adapter, test harness and documentation.
2. **Identity:** login/session lifecycle, scoped roles/permissions, administrator provisioning, MFA/OIDC integration, revocation and access review.
3. **Organization/masters:** hospital/branch setup, module dependencies, effective-dated services/prices/formulary, schedules and policy versions.
4. **Patient:** UHID allocator, search/duplicate scoring, registration, consent/document storage, authorized merge and ABHA linkage boundary.
5. **Scheduling:** capacity/slot transactions, walk-in/online/follow-up, check-in/token, reschedule/cancel, notification outbox.
6. **EMR/prescription:** longitudinal chart, allergy reconciliation, vitals/notes/diagnosis/orders, formulary safety checks, versioned signing/printing.
7. **Billing:** server-priced invoice issue, advances/payments/idempotency, discount/refund approvals, receipt and reconciliation.
8. **Phase exit:** end-to-end SQL integration tests, role/tenant negative tests, concurrency/retry tests, accessibility/responsive tests, privacy review, performance baseline and restore drill.

## Non-negotiable release gates

- No known critical/high exploitable dependency or application finding; threat model and penetration test completed.
- Real SQL Server migration/rollback-forward tests; least-privilege deployment; encrypted secrets/backups; RPO/RTO restore evidence.
- Identity/MFA/session/CSRF and tenant isolation tests; privileged action audit and audit export immutability verified.
- Clinician-approved workflows, terminology/formulary licensing, clinical document retention/amendment policy and downtime procedures.
- Financial reconciliation, duplicate payment/credit tests and approved tax/discount/refund rules.
- LIS/RIS/PACS/ABDM adapters pass their respective conformance/sandbox and reconciliation tests before being advertised as connected.
- Load/soak tests at hospital-specific concurrency; queue/outbox lag and critical-alert delivery monitored.
- Accessibility keyboard/screen-reader review; no patient identifiers in demo assets or routine logs.
- AI features are optional, consent-aware, evaluated, monitored and human-reviewed; never silently prescribe, authorize reports or alter records.

## Ownership and estimates

A realistic program needs product/clinical leads, .NET/backend engineers, frontend/design, QA automation, DBA/platform/security, integration specialists and hospital super-users. Duration depends on chosen scope and integrations; do not treat this roadmap as a fixed calendar commitment. Small-hospital deployments can enable a subset, but shared safety and audit controls remain mandatory.

## Environment note

The workspace has Node.js and a checksum-verified .NET 10 SDK installed outside the repository. SQL Server and unrestricted NuGet access are not available in this sandbox. The runnable preview is the React workspace backed by the synthetic in-memory API adapter. Backend source/migrations must be restored and tested in the intended SQL Server environment; unavailable verification is reported, never replaced with a fabricated success.
