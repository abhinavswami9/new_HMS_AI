# SQL Server data architecture

## Conventions and physical design

Target SQL Server 2022+. Schemas isolate bounded contexts. Initial tables use sequential GUID primary keys (NEWSEQUENTIALID()); high-volume append-only tables may use bigint identity keys where measured access patterns justify them. Tenant-owned tables carry HospitalId and BranchId where operationally relevant. Composite foreign keys prevent cross-hospital references. Money is decimal(19,4); quantities decimal(18,3); instants datetime2(3) UTC. Dates of birth and schedule local dates use date. RowVersion columns and application-owned Version counters protect mutable aggregates. JSON is used only for bounded schema-versioned event/snapshot payloads, not as a substitute for normalized relationships.

Use NVARCHAR for names/clinical text, VARCHAR for ASCII codes/hashes, varbinary for rowversions/document hashes. FK deletes are predominantly NO ACTION; do not cascade away medical history. Explicit retention jobs handle legally approved disposal. Indexes start with tenant + worklist/time/status; tune using actual plans. Large events/results can later be partitioned by UTC date. Reference intervals and terminology are versioned/effective-dated.

## Implemented foundation and Phase 1 schema

The executable initial migration is `database/migrations/0001_phase1.sql`. It creates the foundation, identity, patients, scheduling, EMR, billing, audit and outbox tables. Consult the SQL itself for exact columns/constraints. Principal tables:

| Schema      | Tables                                                                                                              | Purpose                                           |
| ----------- | ------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------- |
| org         | Hospitals, Branches, Departments, ModuleInstallations, Policies, NumberSequences                                    | tenant, location, configuration, scoped numbering |
| iam         | Users, Roles, Permissions, UserRoles, RolePermissions, Memberships, Sessions, LoginEvents                           | authentication and scoped RBAC                    |
| catalog     | Practitioners, Services, Prices, Medicines, PrescriptionTemplates                                                   | versionable clinical/service catalog              |
| patient     | Patients, Contacts, IdentityDocuments, Consents, MergeCases, MergeMappings, Links                                   | demographics, UHID, consent, governed merge       |
| sched       | Schedules, Slots, Appointments, QueueEntries                                                                        | availability, booking, tokens/queue               |
| emr         | Encounters, Allergies, Problems, Vitals, Notes, Diagnoses, Orders, Prescriptions, PrescriptionItems, RecordVersions | longitudinal record, orders and immutable signing |
| bill        | Invoices, Lines, Payments, ApprovalRequests, CreditNotes                                                            | receivables and controlled adjustments            |
| audit       | AuditEvents                                                                                                         | append-only application audit trail               |
| integration | OutboxMessages, InboxMessages                                                                                       | reliable integration boundary                     |
| storage     | Documents                                                                                                           | private object metadata and classification        |
| dbo         | SchemaMigrations                                                                                                    | applied migration history                         |

Critical constraints include unique hospital/UHID, scoped appointment slot, active queue token, invoice number, session token hash and payment-reference idempotency. Clinical/billing writes include a transaction-scoped audit event. Signed document hashes bind canonical content; signing keys/certificates belong in a dedicated signer, not a database plaintext column.

## Implemented Phase 2 schema

Migration `database/migrations/0002_phase2_care.sql` adds the care schemas below. It seeds global Phase 2 permissions and an **unapproved** `nursing.escalation` policy per branch; hospitals must approve the policy through their governed process before nursing observations are accepted. It does not seed synthetic patients, beds, stock or lab tests.

| Schema   | Tables                                                                            | Purpose                                                  |
| -------- | --------------------------------------------------------------------------------- | -------------------------------------------------------- |
| ipd      | Beds, Admissions, BedEvents                                                       | ADT, bed readiness, occupancy history                    |
| nurse    | NursingNotes, MedicationOrders, Administrations, ShiftHandovers, HandoverPatients | nursing care, eMAR, named handover                       |
| pharmacy | StockBatches, StockMovements, Dispenses, DispenseLines, DispenseBatches           | batch stock ledger and prescription/admission dispensing |
| lab      | Tests, Orders, Results                                                            | LIS order/specimen/result lifecycle                      |
| alerts   | CriticalAlerts                                                                    | nursing/laboratory critical alerts                       |

Phase 2 constraints include filtered unique active admission per patient and per bed, composite hospital/branch foreign keys, stock batch versioning, dispensing request-hash idempotency scoped to hospital/branch/actor, lab barcode/accession uniqueness, and immutability triggers for discharged admissions, nursing notes, administrations, stock movements, dispenses, authorized/rejected lab orders, authorized results and acknowledged alerts.

## Full enterprise logical catalog (planned migrations)

These are deliberately separated from implemented tables. Each future module adds its own migration and contract tests. Shared keys/ownership/audit conventions apply throughout.

- **IPD extensions:** Wards, Rooms, BedAssignments, AdmissionRequests, CareTeamAssignments, Transfers, DischargePlans. Core Beds/Admissions/BedEvents are implemented in Phase 2; unique active bed assignment and patient admission are filtered constraints.
- **Nursing extensions:** CarePlans, MedicationSchedules, Escalations. Core NursingNotes, MedicationOrders, Administrations, ShiftHandovers and HandoverPatients are implemented in Phase 2.
- **Pharmacy extensions:** Returns, ReturnItems, Suppliers, PurchaseOrders, PurchaseOrderLines, GoodsReceipts, GoodsReceiptLines. Core StockBatches, StockMovements, Dispenses, DispenseLines and DispenseBatches are implemented in Phase 2.
- **LIS extensions:** Profiles, ProfileTests, Packages, PackageTests, OrderTests, SpecimenEvents, AnalyzerMappings, QcRuns, QcResults, ResultVersions, Reports, ReportVersions, AlertAcknowledgments. Core Tests, Orders, Results and CriticalAlerts are implemented in Phase 2; specimen identity is currently carried on the order.
- **RIS:** Modalities, Orders, Appointments, Studies, ModalityWorklist, ReportTemplates, Reports, ReportVersions, CriticalFindings. StudyInstanceUID and accession are distinct identifiers; no image BLOB in OLTP.
- **Emergency:** Registrations, TriageAssessments, Observations, Dispositions, Transfers; linked encounter, unknown identity reconciliation and triage scale version.
- **OT:** Theatres, Bookings, Schedules, TeamAssignments, PreopAssessments, Checklists, ChecklistResponses, TimeEvents, AnaesthesiaNotes, OperationNotes, Implants, Consumables, RecoveryEvents. Procedure/site/laterality and implant lot/serial trace.
- **Blood bank:** Donors, Donations, EligibilityAssessments, ScreeningTests, Components, ComponentEvents, Inventory, Crossmatches, Issues, Returns, Reactions, Disposal. Donation-to-component parentage and recipient chain.
- **Insurance:** Payers, Plans, Coverage, Preauthorizations, PreauthLines, Claims, ClaimLines, ClaimEvents, Settlements, SettlementLines, Rejections, Appeals.
- **Inventory/CSSD:** Stores, Items, Batches, StockMovements, Indents, IndentLines, Approvals, PurchaseOrders, GoodsReceipts, Transfers, Consumption, Assets, AssetCustody; InstrumentSets, SetItems, SterilizationCycles, CycleItems, Indicators, Issues, Returns, Recalls.
- **Incentives:** RuleSets, Rules, CalculationBatches, CalculationLines, InputSnapshots, Approvals, PaymentBatches, Payments, Adjustments.
- **Telemedicine:** Sessions, Participants, WaitingRoomEvents, Consents, Uploads, ProviderEvents; short-lived tokens hashed, recording metadata only if explicitly enabled.
- **Quality:** Infections, Incidents, RiskAssessments, CAPA, CAPAActions, ClinicalAudits, AuditFindings, IndicatorDefinitions, IndicatorValues, Documents, DocumentVersions, SOPAcknowledgments.
- **Portal:** Accounts, PatientLinks, ProxyGrants, DeviceSessions, Preferences; proxy grant scopes and revocation.
- **Reporting:** Definitions, Versions, Executions, Exports, Subscriptions; access-scoped aggregate/read models with metric definition and freshness.

## Representative relationships

```text
Hospital 1──* Branch 1──* Membership *──1 User
Patient 1──* Encounter 1──* Order 1──0..1 Specimen 1──* Result
Encounter 1──* Prescription 1──1..* PrescriptionItem
Encounter 1──0..1 Admission 1──* BedAssignment *──1 Bed
Order 1──0..1 ImagingStudy ──* ReportVersion
Invoice 1──* InvoiceLine *──1 source charge
Invoice 1──* PaymentAllocation *──1 Payment
Donation 1──* BloodComponent 1──* Issue *──1 Encounter
AuditEvent ── logical entity reference + version + actor context
```

## Query/index plan

- Patient search: HospitalId + normalized phone; HospitalId + name/DOB; exact UHID unique. Parameterized prefix search initially; governed search projection for large registries. Avoid leading-wildcard scans at scale.
- Queue: BranchId, DoctorId, LocalServiceDate, Status INCLUDE(token, patient, time).
- Encounters: HospitalId, PatientId, StartedAtUtc DESC; assigned doctor/status worklist.
- Orders/specimens: BranchId, Status, OrderedAtUtc; barcode unique; TAT milestone events.
- Invoices: BranchId, Status, IssuedAtUtc; patient balance projection; payment external reference unique within provider scope.
- Audit: HospitalId, EntityName, EntityId, OccurredAtUtc; time-based retention/archive plan.
- Outbox: Status, NextAttemptUtc, CreatedAtUtc; claim with UPDATE OUTPUT/READPAST locking, bounded batches, retry backoff.

## Deployment and database security

Migrations run under a deployment identity, not the application login. Application role receives required SELECT/INSERT/UPDATE and EXECUTE only; no general DELETE on clinical/audit tables. Audit has INSERT/SELECT only. Separate reporting identity and redacted views. Enable backup encryption and test restore. SQL parameterization is mandatory; report builder cannot execute arbitrary user SQL. Snapshot/replication choices must preserve tenant isolation. Seed production with configuration only; synthetic records and demo credentials must never enter production.
