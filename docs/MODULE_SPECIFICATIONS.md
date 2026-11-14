# Module contracts and role model

## Contract convention

Each module below specifies architecture/ownership, roles, tables, API surface, screens, business rules, workflow, validation and audit. Endpoints are relative to `/api/v1`; `{id}` is an external GUID. Collection endpoints are tenant-scoped and bounded or paginated according to the delivered phase. Retryable writes require an idempotency mechanism where implemented and RowVersion/Version checks where mutable; see PHASE1_STATUS.md for exact coverage. Permissions are checked server-side. `planned` means a design contract, not an implemented endpoint. Consult README.md for the exact delivered slice.

Shared conventions: master records are hospital-scoped, effective-dated where clinically meaningful, and deactivated rather than deleted after use. Every change records actor, UTC time, hospital/branch, entity/version, action, reason and minimum-necessary, access-controlled change metadata. Clinical record access is also audited. Approval requests cannot be approved by their requester unless hospital policy explicitly permits a low-risk exception; clinical sign-off is never delegated by that exception.

## Permission matrix

| Role            | Default capabilities (least privilege; configurable grants)                         |
| --------------- | ----------------------------------------------------------------------------------- |
| Super Admin     | platform configuration, hospital provisioning; no implicit clinical sign-off        |
| Hospital Admin  | scoped setup, masters, module policy, user/role administration, audit review        |
| Doctor          | assigned patient charts, encounters, prescriptions, orders, referrals, own schedule |
| Nurse           | assigned ward/encounter nursing assessment, vitals, administration, handover        |
| Receptionist    | registration, scheduling, queue, consent capture; no clinical signing               |
| Lab Technician  | accession, collection, processing, result entry; no final pathologist authorization |
| Pathologist     | lab validation/authorization, amendments, lab quality review                        |
| Radiologist     | imaging interpretation, report approval/addenda                                     |
| Pharmacist      | dispensing, stock, returns, medication alerts; no prescribing                       |
| OT Staff        | assigned OT schedule/checklists/implants/counts; clinical notes by qualification    |
| Billing Staff   | charges, receipts, advances, refund/discount requests                               |
| Store Manager   | vendors, procurement, receipts, transfers, assets                                   |
| Accountant      | reconciliation, settlements, credit notes, financial reports/approvals              |
| Quality Manager | incidents, CAPA, document control, audits, quality indicators                       |
| Patient         | own verified record, consent, booking, payments and authorized downloads            |

Additional explicit permissions: `patient.merge`, `patient.breakglass`, `billing.discount.approve`, `billing.refund.approve`, `incentive.approve`, `security.admin`, `audit.read`, `consent.manage`, `clinical.sign`, `lab.authorize`, `radiology.authorize`. Roles are customizable; permission names and protected invariants are not arbitrary client strings. Branch/department/assignment restrictions narrow grants. Emergency access requires a reason, notifies reviewers and does not bypass authentication.

## Phase 1

### 1. Authentication and RBAC

1. **Architecture:** Identity module; server sessions, password policy, membership/role grants, authentication and authorization ports.
2. **Roles:** Super Admin, Hospital Admin; every authenticated principal.
3. **Tables:** iam.Users, Roles, Permissions, UserRoles, RolePermissions, Memberships, Sessions, LoginEvents; audit.AuditEvents.
4. **API:** POST auth/login, auth/logout; GET auth/me; GET/POST admin/users, admin/roles; PUT admin/roles/{id}/permissions; POST admin/users/{id}/revoke-sessions. Enterprise MFA/password reset endpoints are planned.
5. **Screens:** sign in, account/session, user directory, role editor, access review.
6. **Rules:** disabled users cannot log in; hashes never leave the server; random tokens are hashed at rest; tenant/permissions come from membership; privilege changes revoke affected sessions.
7. **Workflow:** provision → membership/role → authenticate → scoped session → use/revoke/expire.
8. **Validation:** unique normalized username; bounded inputs; strong password; valid scoped role IDs; prevent removing last active administrator without replacement.
9. **Audit:** success/failure, source, revocation, user/role/membership changes; no passwords/tokens/PHI in event payloads.

### 2. Hospital setup and module configuration

1. **Architecture:** Organization module owns hospitals, branches, departments, rooms and policy versions; module registry consumes entitlements.
2. **Roles:** Super Admin, Hospital Admin.
3. **Tables:** org.Hospitals, Branches, Departments, ModuleInstallations, Policies, NumberSequences.
4. **API:** GET/POST hospitals; GET/PUT branches/{id}; GET/PUT branches/{id}/modules; GET/PUT branches/{id}/policies.
5. **Screens:** organization profile, branch selector, department setup, module switches, policy editor.
6. **Rules:** dependencies checked before activation; disabling a module hides new work, not existing records; policy changes are versioned.
7. **Workflow:** hospital → branch → departments → masters → modules/policies → staff membership.
8. **Validation:** unique codes, timezone/currency, dependency closure, effective dates, authorized approvers.
9. **Audit:** before/after policy values, reason, approver and effective instant.

### 3. Master data and service catalog

1. **Architecture:** Masters module; versioned department/practitioner/service/medicine/terminology references.
2. **Roles:** Hospital Admin, Accountant; clinical terminology governance by qualified designees.
3. **Tables:** catalog.Services, PriceLists, Prices, Medicines, Templates, DiagnosisCodes, Suppliers; org.Departments.
4. **API:** GET services/medicines/departments/practitioners; POST/PUT admin/services/{id}; POST admin/price-lists; PUT admin/prices/{id}.
5. **Screens:** service/medicine search, price list, practitioner schedules, template and code-system management.
6. **Rules:** prices have currency/effective interval; encounters snapshot service name/price; medicines distinguish generic/brand; code-system provenance retained.
7. **Workflow:** draft → clinical/finance review → activate → version/deactivate.
8. **Validation:** no overlapping active price for same scope; nonnegative amounts; unit/frequency constraints; licensed terminology import.
9. **Audit:** catalog and price version history, import batch/checksum and actor.

### 4. Patient registration and UHID

1. **Architecture:** Patient module + MPI matching adapter; private document storage; consent ledger.
2. **Roles:** Receptionist, Hospital Admin; merge reviewer; Patient for permitted portal updates.
3. **Tables:** patient.Patients, Contacts, IdentityDocuments, Consents, MergeCases, Links; storage.Documents.
4. **API:** GET/POST patients; GET/PUT patients/{id}; POST patients/duplicate-check; POST patients/merge-requests; POST patients/merge-requests/{id}/approve; POST patients/{id}/consents; POST patients/{id}/documents.
5. **Screens:** registry/search, quick/new registration, duplicate review, patient banner/chart, consent/document tabs, merge review.
6. **Rules:** hospital-unique UHID; probable duplicates reviewed, not silently merged; survivor/golden record chosen explicitly; ABHA is verified linkage, not a typed placeholder; original identifiers retained.
7. **Workflow:** search → register/duplicate review → UHID → consent → appointment/OPD/emergency/IPD. Merge: request → independent approval → atomic relinking → audit → notification.
8. **Validation:** name/DOB/sex/contact, plausible dates, document formats, mandatory consent purpose, merge reason; do not reject patients solely for missing national ID or phone.
9. **Audit:** registration/update/read, document access, consent capture/withdrawal, match evidence and every merge mapping.

### 5. Appointment and queue

1. **Architecture:** Scheduling module; slot capacity and queue aggregates; notification outbox.
2. **Roles:** Receptionist, Doctor, Patient.
3. **Tables:** sched.Schedules, ScheduleExceptions, Slots, Appointments, QueueEntries.
4. **API:** GET/POST appointments; POST appointments/{id}/check-in, cancel, reschedule; GET queue; GET/PUT admin/schedules; POST admin/schedules/{id}/exceptions.
5. **Screens:** day/calendar, booking, doctor availability, live queue/token board, reschedule/cancel dialog.
6. **Rules:** capacity/leave constraints; unique slot allocation; scoped token sequence; cancellation retains history; follow-up links prior encounter; online booking cannot override blocked time.
7. **Workflow:** availability → book/walk-in → confirm → check-in/token → consult → complete; alternate cancel/reschedule.
8. **Validation:** active practitioner/service, bounded future horizon, timezone conversion, non-overlapping allocation, reason for cancellation.
9. **Audit:** booking/reschedule/cancel/check-in, actor/channel and notification intent/delivery separately.

### 6. OPD and doctor EMR

1. **Architecture:** Encounter module owns visits and signed clinical documents; reads Patient history and diagnostic summaries through contracts.
2. **Roles:** Doctor, Nurse (vitals), assigned care team; scoped read-only billing access where permitted.
3. **Tables:** emr.Encounters, Allergies, Problems, Vitals, Notes, Diagnoses, TreatmentPlans, Referrals, Orders, RecordVersions.
4. **API:** GET patients/{id}/chart; GET/POST encounters; PUT encounters/{id}/draft; POST encounters/{id}/vitals, allergies, notes, orders; POST encounters/{id}/sign; POST encounters/{id}/addenda.
5. **Screens:** doctor dashboard, longitudinal chart, visit workspace, vitals/allergies/history, diagnosis/plan, order entry, prior prescriptions/reports/admissions/discharge summaries.
6. **Rules:** only assigned qualified signer finalizes; allergy review before prescribing; signed content immutable; amendment/addendum links original; no diagnosis code inferred without review.
7. **Workflow:** arrive → history/allergy reconciliation → vitals/examination → diagnosis/plan → prescriptions/orders → sign → follow-up/referral.
8. **Validation:** required encounter/patient linkage, plausible vital ranges with override reason, coded diagnosis system, mandatory signer and finalization checks.
9. **Audit:** chart reads, draft versions, allergy reconciliation, order changes, signature metadata, addenda and break-glass.

### 7. Digital prescription

1. **Architecture:** Encounter prescription document + formulary/allergy interaction ports; PDF renderer and signature adapter.
2. **Roles:** Doctor signs; Pharmacist dispenses; Patient views authorized copy.
3. **Tables:** emr.Prescriptions, PrescriptionItems, Templates, AlertOverrides, RecordVersions; catalog.Medicines.
4. **API:** POST encounters/{id}/prescriptions/draft; PUT prescriptions/{id}; POST prescriptions/{id}/sign, addenda; GET prescriptions/{id}/print; GET formulary.
5. **Screens:** template picker, medicine search/editor, allergy/interactions panel, signed printable prescription.
6. **Rules:** generic/brand identity retained; exact dose/route/frequency/duration/instructions; allergy alert cannot be silently ignored; interaction knowledge requires licensed/versioned source; digital signing requires actual signature infrastructure.
7. **Workflow:** draft → safety review → explicit override reason if permitted → sign → print/PDF → dispensing linkage.
8. **Validation:** positive dose/duration, recognized units/frequency, active patient/encounter, required allergy review, no signing empty prescription.
9. **Audit:** item changes, alert evidence/override, signer/time, document hash, print/export and amendment linkage.

### 8. Billing and revenue cycle

1. **Architecture:** Billing module consumes priced charge events; invoice/payment/refund aggregates; outbox to ledger/reporting.
2. **Roles:** Billing Staff, Accountant, Hospital Admin; approval roles explicitly granted.
3. **Tables:** bill.Invoices, Lines, Payments, Allocations, Advances, Refunds, CreditNotes, Packages, ApprovalRequests.
4. **API:** GET/POST invoices; POST invoices/{id}/issue, payments, credit-notes; POST discounts/request; POST discounts/{id}/approve; GET receipts/{id}/print.
5. **Screens:** billing queue, invoice builder, receipt/payment dialog, advances, refund/credit request, approval inbox, day close.
6. **Rules:** server prices/total calculation; issued invoice immutable; payments idempotent; overpayment held as advance or rejected by policy; discounts/refunds require authorized approval; no silent deletion.
7. **Workflow:** charge capture → review/approval → issue → advance/payment → allocation → receipt → reconciliation; refund/credit separately.
8. **Validation:** tenant-owned charge sources, nonnegative quantities/prices, currency consistency, amount > 0, outstanding balance, payment reference uniqueness.
9. **Audit:** line source, price version, approval reason, issue hash, receipt, allocation, refund and reconciliation changes.

## Phase 2

> Delivery boundary: the initial Phase 2 vertical slice is implemented. See [PHASE2_STATUS.md](PHASE2_STATUS.md) for delivered behavior and remaining enterprise gates. The specifications below remain the target contract.

### 9. IPD / ADT

1. **Architecture:** Admission module, bed allocation saga with compensating reservation, care-team contracts.
2. **Roles:** Doctor, Nurse, Receptionist, Bed Manager, Billing Staff.
3. **Tables:** ipd.Admissions, AdmissionRequests, CareTeamAssignments, Transfers, DailyNotes, DischargePlans, DischargeSummaries.
4. **API:** POST admissions/requests, admissions; POST admissions/{id}/transfer, discharge-plan, discharge; GET admissions/{id}/timeline.
5. **Screens:** admission desk, bed allocation, care team, daily rounds, discharge checklist/summary.
6. **Rules:** one active admission per patient/branch; bed capacity invariant; discharge requires clinical and configured financial clearances; transfers preserve history.
7. **Workflow:** request → allocate bed → admit/assign → care/orders/procedures → plan → clearance → sign summary → discharge.
8. **Validation:** bed class/acuity, attending clinician, admission/discharge times, no overlapping bed occupancy.
9. **Audit:** every ADT event, bed/care-team change, clearance and signed summary version.

### 10. Bed management

1. **Architecture:** Admission bed/ward aggregate; real-time projection via versioned events.
2. **Roles:** Bed Manager, Nurse, Hospital Admin.
3. **Tables:** ipd.Wards, Rooms, Beds, BedAssignments, StatusEvents.
4. **API:** GET beds/board; PUT beds/{id}/status; POST beds/{id}/reserve, release; GET beds/{id}/history.
5. **Screens:** visual board by ward/room, status filters, reservation and housekeeping queue.
6. **Rules:** available/occupied/reserved/cleaning/maintenance states; ICU/private/semi-private/ward class; atomic allocation; stale updates rejected.
7. **Workflow:** available → reserve/occupy → vacate → clean/inspect → available; maintenance branch.
8. **Validation:** allowed transitions, reservation owner/expiry, isolation/acuity compatibility, reason for maintenance.
9. **Audit:** status/assignment changes with timestamps, actor and reason; occupancy reconstruction.

### 11. Nursing

1. **Architecture:** Nursing module tied to admission/assignment; medication administration events.
2. **Roles:** Nurse, Charge Nurse, Doctor.
3. **Tables:** nurse.Assessments, Notes, Vitals, IntakeOutput, MedicationAdministrations, Handover, Alerts, Escalations.
4. **API:** GET nursing/worklist; POST admissions/{id}/assessments, notes, intake-output, administrations; POST handovers/{id}/accept; POST alerts/{id}/escalate.
5. **Screens:** ward worklist, assessment, observation/intake-output charts, eMAR, handover and escalation inbox.
6. **Rules:** patient/drug/dose/route/time checks; document refused/held/missed doses; acceptance of handover; escalation timers configurable.
7. **Workflow:** assess → schedule → administer/observe → alert/escalate → handover/accept.
8. **Validation:** active order, qualified administrator, units/ranges, mandatory hold reason, recipient iam.
9. **Audit:** administration provenance, corrections, alert acknowledgment/escalation and handover acceptance.

### 12. Pharmacy

1. **Architecture:** Pharmacy module; prescription fulfillment and batch-stock ledger; procurement integration.
2. **Roles:** Pharmacist, Store Manager, Doctor (read fulfillment), Billing Staff.
3. **Tables:** pharm.Dispenses, DispenseItems, StockBatches, StockMovements, Returns, PurchaseOrders, GoodsReceipts; catalog.Suppliers/Medicines.
4. **API:** GET pharmacy/worklist; POST dispenses, returns; GET stock/alerts; POST purchase-orders, goods-receipts; POST dispenses/{id}/verify.
5. **Screens:** OP/IP dispensing, batch/expiry search, stock ledger, purchasing/GRN, supplier and return desks.
6. **Rules:** valid signed prescription; batch traceability; configurable FEFO; no negative stock; controlled-drug policy; independent verification where required; recall trace.
7. **Workflow:** prescription → availability/safety check → dispense/verify → bill/stock movement → return/reconciliation.
8. **Validation:** batch/expiry/quantity, unit conversion, return reason, supplier authorization, cold-chain evidence.
9. **Audit:** prescription link, dispensing pharmacist, batch movements, overrides, returns and recall actions.

### 13. Laboratory / LIS

1. **Architecture:** Lab module, ordered specimen lifecycle, analyzer inbox, validation/authorization and report renderer.
2. **Roles:** Doctor, Lab Technician, Pathologist, Quality Manager.
3. **Tables:** lab.Tests, Profiles, PackageTests, Orders, Specimens, Events, Results, Validations, Reports, CriticalAlerts, QcRuns, AnalyzerMappings.
4. **API:** POST lab/orders, specimens/{id}/collect, accession, reject, results; POST results/{id}/validate; POST reports/{id}/authorize; GET lab/worklists, tat, qc.
5. **Screens:** order/collection desk, barcode labels, accession/tracking, technician bench, validation/pathologist queue, reports, TAT/QC dashboard.
6. **Rules:** chain of custody; rejection reason; method/unit/reference interval provenance; delta/critical checks; separation of entry/validation/authorization per policy; immutable final report and addenda; configurable NABL-oriented QC.
7. **Workflow:** order → collect/barcode → accession/process → analyzer/manual result → technical validation → authorization → report/delivery; reject/recollect branch.
8. **Validation:** patient/specimen match, sample type/tube/volume, collection time, QC acceptance, units/ranges, mandatory critical notification/acknowledgment.
9. **Audit:** every specimen transition, raw analyzer payload reference/checksum, result edits, validation, signature, report access and alert acknowledgment.

## Phase 3

### 14. Radiology / RIS

1. **Architecture:** Imaging order/scheduling/reporting module + modality worklist and PACS ports.
2. **Roles:** Doctor, Radiology Receptionist, Technician, Radiologist.
3. **Tables:** ris.Orders, Appointments, ModalityWorklist, Studies, ReportTemplates, Reports, CriticalFindings.
4. **API:** POST radiology/orders, appointments; GET modalities/{id}/worklist, radiologist/worklist; POST studies/{id}/reports; POST reports/{id}/approve, addenda.
5. **Screens:** scheduling, modality/technician worklist, structured report editor, approval queue, critical findings.
6. **Rules:** accession links order/study; identity reconciliation; protocol/pregnancy/contrast safety checks as configured; authorized final reports; addenda never overwrite.
7. **Workflow:** order → schedule → MWL → acquisition → PACS → interpret → approve → report; critical alert branch.
8. **Validation:** modality/body part, laterality, accession uniqueness, required template fields, qualified signer.
9. **Audit:** schedule/access/study linkage/report versions, critical communication and approval.

### 15. PACS / DICOM integration

1. **Architecture:** Imaging adapter for DICOM MWL/MPPS and DICOMweb; external archive and viewer launch broker.
2. **Roles:** Technician, Radiologist, Doctor; integration service iam.
3. **Tables:** integration.Endpoints, StudyLinks, ViewerSessions, MessageInbox, MessageOutbox.
4. **API:** GET studies/{id}/viewer-launch; POST integration/dicom/mpps; GET integration/studies/{id}/status; DICOMweb routes through authenticated gateway.
5. **Screens:** study status, EMR image launch, integration reconciliation/monitor.
6. **Rules:** no untrusted viewer URL; short-lived scoped launch; reconcile patient/accession; preserve StudyInstanceUID; no diagnostic display certification assumed.
7. **Workflow:** MWL → acquisition/MPPS → archive association → viewer launch → report linkage.
8. **Validation:** DICOM UIDs/tags, endpoint allowlist, TLS, authorized study, payload limits.
9. **Audit:** message IDs, study access, viewer launch and reconciliation decisions; minimize pixel metadata in logs.

### 16. Emergency / casualty

1. **Architecture:** Emergency encounter extension with triage and observation states.
2. **Roles:** Triage Nurse, Emergency Doctor, Receptionist, Billing Staff.
3. **Tables:** emergency.Registrations, TriageAssessments, Observations, Dispositions, Referrals.
4. **API:** POST emergency/registrations, {id}/triage, observations, disposition; GET emergency/board.
5. **Screens:** rapid registration, severity board, resuscitation/observation chart, disposition.
6. **Rules:** treatment not blocked by missing identity/payment in configured emergency policy; unknown-patient identity reconciliation; reassessment timers; prioritized queue.
7. **Workflow:** quick register → triage → treatment/medication/investigation → observe → admit/refer/discharge.
8. **Validation:** triage scale/version, time ordering, reassessment reason, disposition and receiving facility.
9. **Audit:** identity reconciliation, triage/reassessment, overrides, disposition and transfer handoff.

### 17. Operation theatre

1. **Architecture:** Theatre scheduling/procedure record; staff/resource reservations; implant/consumable traceability.
2. **Roles:** Surgeon, Anaesthetist, OT Nurse, OT Coordinator, Store/Billing.
3. **Tables:** ot.Bookings, Schedules, TeamAssignments, PreopAssessments, Checklists, AnaesthesiaNotes, OperationNotes, Implants, Consumables, RecoveryEvents.
4. **API:** POST ot/bookings, {id}/schedule, checklists, time-in, time-out, notes, implants; POST ot/{id}/complete; GET ot/calendar, utilization.
5. **Screens:** booking/calendar, team/theatre allocation, pre-op/checklist, intra-op record, recovery and utilization.
6. **Rules:** consent/pre-op requirements configurable; surgical safety checklist/time-out; resource/staff conflict prevention; lot/implant trace; actual times drive utilization.
7. **Workflow:** decision → assessment/consent → booking/scheduling → surgery/checklists → recovery → post-op → charges.
8. **Validation:** procedure/laterality/site, team qualifications, implant identifiers, count reconciliation, chronological events.
9. **Audit:** schedule/team changes, checklist responses, time events, notes/signatures, implants and consumables.

### 18. Blood bank

1. **Architecture:** Blood module with donor-to-component-to-recipient traceability and compatibility rules.
2. **Roles:** Donor Counselor, Blood Technician, Authorized Medical Officer, Nurse.
3. **Tables:** blood.Donors, Donations, ScreeningTests, Components, Inventory, Crossmatches, Issues, Returns, Reactions, Disposal.
4. **API:** POST blood/donors, donations, components, crossmatches, issues, returns; GET blood/inventory, expiry.
5. **Screens:** donor screening, collection/testing, component inventory, crossmatch/issue, reaction/disposal reports.
6. **Rules:** eligibility/testing per configured policy; group/component compatibility; quarantine until release; cold chain; recipient bedside verification; returns assessed, not automatically restocked.
7. **Workflow:** donor → collect/test/group → separate → release inventory → request/crossmatch → issue/transfuse/return/dispose.
8. **Validation:** donor/unit identifiers, expiry, test results, compatibility, recipient consent and transfusion indication.
9. **Audit:** complete chain of custody, temperature excursions, release/issue/return decisions and adverse reactions.

## Phase 4

### 19. Insurance / TPA

1. **Architecture:** Coverage/claim module linked to encounters/invoices; payer adapter and approval workflow.
2. **Roles:** Insurance Coordinator, Billing Staff, Accountant, Patient.
3. **Tables:** insurance.Payers, Plans, Coverage, Preauthorizations, Claims, ClaimLines, Settlements, Rejections.
4. **API:** POST coverage/verify, preauthorizations, claims; POST claims/{id}/submit, respond, settlements; GET claims/worklist.
5. **Screens:** eligibility, pre-auth, claim editor/tracker, query/rejection and settlement reconciliation.
6. **Rules:** coverage dates/limits/exclusions; approved package variance; no duplicate claim submission; patient liability transparent; payer response evidence retained.
7. **Workflow:** register coverage → eligibility/pre-auth → services → claim → query/response → settlement/rejection/appeal.
8. **Validation:** policy/member identity, diagnosis/procedure requirements, authorization expiry, amount/line reconciliation.
9. **Audit:** coverage evidence, authorization, claim versions/submissions, payer responses and financial adjustments.

### 20. Doctor incentives

1. **Architecture:** Incentive rules engine consuming eligible revenue/clinical events; approval and payment batch.
2. **Roles:** Accountant, Hospital Admin, Doctor (own statement), authorized approver.
3. **Tables:** incentive.RuleSets, Rules, CalculationBatches, Lines, Approvals, Payments, Adjustments.
4. **API:** POST admin/incentive-rules; POST incentive/batches/calculate, {id}/approve, payments; GET doctors/{id}/statement.
5. **Screens:** rule builder, monthly calculation/review, approval queue, payment report.
6. **Rules:** OP/IP/procedure/surgery fixed/percentage/revenue share; precedence/department/doctor scope; effective dates; net of refunds per policy; no overlapping ambiguous rule; snapshot inputs.
7. **Workflow:** configure/approve rule → period close → calculate/review → approve → pay/reconcile.
8. **Validation:** percentage bounds, nonnegative fixed amount, eligible basis, period lock, separation of calculation/approval.
9. **Audit:** rule/version/input snapshot, recalculation reason, approval and payment reference.

### 21. Telemedicine

1. **Architecture:** Scheduling/Encounter extension + video provider adapter, waiting-room authorization and uploads.
2. **Roles:** Patient, Doctor, Receptionist.
3. **Tables:** tele.Sessions, Participants, WaitingRoomEvents, Uploads, ConsentEvents; sched.Appointments/emr.Encounters.
4. **API:** POST tele/sessions/{id}/join-token, admit, end; POST tele/sessions/{id}/documents; GET tele/history.
5. **Screens:** patient waiting room, doctor tele queue, consultation/prescription, uploads/history, payment status.
6. **Rules:** identity/consent/modality suitability; ephemeral room tokens; no unauthorized recording; emergency escalation instructions; applicable telemedicine documentation policy.
7. **Workflow:** book/pay if enabled → consent/wait → admit → consult/prescribe → follow-up/history.
8. **Validation:** appointment time/participant, consent, upload safety, token expiry, consultation participants.
9. **Audit:** consent/join/admit/end, prescription/document access; recording only with explicit policy/consent.

### 22. Inventory and procurement

1. **Architecture:** Stock ledger across central/department stores; purchasing/approval workflow; asset register.
2. **Roles:** Store Manager, Department Head, Accountant, Procurement Approver.
3. **Tables:** inventory.Stores, Items, Batches, Movements, Indents, Approvals, PurchaseOrders, GoodsReceipts, Transfers, Consumption, Assets.
4. **API:** POST indents, {id}/approve, purchase-orders, goods-receipts, transfers, consumption; GET stock/alerts, assets.
5. **Screens:** stock dashboard, indent/approval, vendor/PO/GRN, transfer/consumption, assets/expiry.
6. **Rules:** no negative stock; lot/expiry/serial trace; receipt inspection; three-way match as configured; transfer dispatch/receipt reconciliation; valuation policy versioned.
7. **Workflow:** indent → approval → PO → receive/inspect → stock → issue/transfer/consume → reconcile.
8. **Validation:** UOM conversion, batch/expiry, authorized vendor, quantity tolerances, serial uniqueness.
9. **Audit:** stock movements, price/valuation changes, approvals, receipt discrepancies and asset custody.

### 23. CSSD

1. **Architecture:** Instrument/set lifecycle linked to sterilizer cycles and theatre issue.
2. **Roles:** CSSD Technician, OT Nurse, Quality Manager.
3. **Tables:** cssd.InstrumentSets, Items, Cycles, CycleItems, Indicators, Issues, Returns, Recalls.
4. **API:** POST cssd/receive, cycles, {id}/release, issues, returns, recalls; GET cssd/trace/{id}.
5. **Screens:** decontamination queue, cycle load/indicator entry, sterile storage, issue/return and recall trace.
6. **Rules:** dirty→clean→sterile states; failed cycle quarantines load; release criteria configurable; trace set/cycle to patient/procedure; recall affected issues.
7. **Workflow:** return/receive → clean/pack → sterilize/indicators → release/store → issue/use → return.
8. **Validation:** set completeness, cycle parameters, indicator result, expiry, recipient and return condition.
9. **Audit:** every custody/state transition, cycle evidence, release authority and recall closure.

## Phase 5

### 24. Infection control, quality and accreditation workflows

1. **Architecture:** Quality module; configurable surveillance definitions, incidents, CAPA and controlled documents.
2. **Roles:** Quality Manager, Infection Control Nurse, Department Head, all staff for reporting.
3. **Tables:** quality.Infections, Incidents, RiskAssessments, CAPA, ClinicalAudits, Indicators, Documents, DocumentVersions, SOPAcknowledgments.
4. **API:** POST quality/incidents, infections, capa, audits; POST documents/{id}/approve, acknowledge; GET quality/indicators.
5. **Screens:** incident/infection reporting, risk register, CAPA board, audit planner, SOP library, indicator dashboard.
6. **Rules:** NABH/NABL-oriented evidence, not hard-coded accreditation claims; policy/version/definition-driven indicators; confidentiality and just-culture access; effectiveness verification before CAPA closure.
7. **Workflow:** report/surveil → assess → assign CAPA → implement → verify; document draft→review→approve→publish→acknowledge→retire.
8. **Validation:** numerator/denominator definitions, reporting period, responsible owner/due date, approval and acknowledgment iam.
9. **Audit:** report access/amendments, investigation/CAPA evidence, document versions, audit findings and indicator calculation inputs.

### 25. Patient portal

1. **Architecture:** Separate patient audience and BFF policies over Patient/Scheduling/Encounter/Billing/Telemedicine contracts.
2. **Roles:** Patient, verified proxy with scoped/expiring consent.
3. **Tables:** portal.Accounts, PatientLinks, ProxyGrants, DeviceSessions, Preferences; existing clinical/financial records.
4. **API:** GET portal/profile, appointments, prescriptions, reports, bills, summaries; POST portal/appointments, payments, consents; GET portal/documents/{id}/download.
5. **Screens:** verified profile, record timeline, appointments/follow-up, reports/prescriptions, bills/payments, consent/proxy management.
6. **Rules:** strong identity proofing for linkage; adolescent/proxy rules configurable; only authorized/final reports per policy; sensitive-category segmentation; revocable proxy access.
7. **Workflow:** verify/link → consent/preferences → view/book/pay/download → manage access.
8. **Validation:** ownership on every object, proofing evidence, consent scope/expiry, upload safety.
9. **Audit:** linkage, login, every record/download/payment and proxy grant/revocation.

### 26. Management dashboards and reporting

1. **Architecture:** Reporting read models over governed events/replicas; report definitions and export worker.
2. **Roles:** Management, Department Head, Doctor, Accountant, Quality Manager with scoped grants.
3. **Tables:** report.Definitions, Versions, Executions, Exports, Subscriptions; analytics aggregate tables.
4. **API:** GET dashboards/clinical, financial, management; POST reports/execute, exports; GET reports/executions/{id}.
5. **Screens:** management/clinical/financial dashboards, report builder, date/scope filters, export history.
6. **Rules:** explicit metric definitions/timezone; distinguish provisional/final data; no arbitrary SQL from users; exports inherit access and are watermarked/audited; freshness shown.
7. **Workflow:** select approved definition → scope/filter → execute → review → authorized PDF/Excel export.
8. **Validation:** allowed fields/filters, date bounds, row/export limits, scoped parameters, definition version.
9. **Audit:** report definition changes, execution parameters, row counts, export/download and subscription delivery.

## Cross-module acceptance gates

Every increment ships its schema migration, permission map, validation, audit assertions, failure-path tests, UI empty/error/loading states and operator documentation. Clinical modules additionally require clinician-reviewed workflows and test data covering identity mismatch, concurrency, amendments and critical alerts. Financial modules require balanced-ledger and duplicate-request tests. Integration modules require contract tests, retry/dead-letter handling and reconciliation. No module is promoted to production solely because its screens exist.
