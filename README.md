# Careflow HIS

A modular hospital information system foundation: **ASP.NET Core / .NET 10, SQL Server, React + TypeScript**, with a responsive clinical workspace and an explicitly synthetic development preview.

> **Delivery status:** architecture + Phase 1 and Phase 2 vertical slices. This is not a certified, production-ready hospital platform. The live preview uses synthetic records and a development-only in-memory adapter behind the real .NET API. The full product runs as one ASP.NET Core process against SQL Server: the migrations and Phase 1/Phase 2 data paths are exercised against a real SQL Server container in CI (`sql-integration` job) and by `tests/Careflow.SqlTests`. Do not enter real patient information into the preview.

## Start here

| Document                                               | Contents                                                                                                  |
| ------------------------------------------------------ | --------------------------------------------------------------------------------------------------------- |
| [System architecture](docs/ARCHITECTURE.md)            | modular architecture, tenancy, security, consistency, interoperability and operations                     |
| [Module specifications](docs/MODULE_SPECIFICATIONS.md) | every requested module: architecture, roles, tables, APIs, screens, rules, workflow, validation and audit |
| [Database architecture](docs/DATABASE.md)              | implemented schema, enterprise logical catalog, relationships, indexing and deployment controls           |
| [Roadmap](docs/ROADMAP.md)                             | five-phase delivery plan, current scope and production release gates                                      |
| [Phase 1 boundaries](docs/PHASE1_STATUS.md)            | exactly what works, what is partial and what remains planned                                              |
| [Phase 2 boundaries](docs/PHASE2_STATUS.md)            | IPD/ADT, beds, nursing/eMAR, pharmacy and LIS delivery boundary                                           |
| [Phase 3 boundaries](docs/PHASE3_STATUS.md)            | RIS (no live PACS), emergency, operation theatre and blood bank delivery boundary                          |
| [Phase 4 boundaries](docs/PHASE4_STATUS.md)            | insurance/TPA, incentives, telemedicine (no video provider), inventory and CSSD delivery boundary          |
| [Deployment runbook](docs/DEPLOYMENT.md)               | SQL migration, administrator/clinician provisioning, configuration and release checks                     |

## Working Phase 1 slice

- Secure cookie sessions, password hashing, login history, revocation and server-enforced role permissions.
- Patient registration with hospital-scoped UHID, duplicate review, search and audited chart access.
- Appointment booking, overlap rejection, rescheduling/cancellation, check-in and token allocation.
- Assigned-doctor EMR drafts: vitals, complaints, examination, diagnosis, plan, allergies, prescription and investigation orders.
- Explicit allergy reconciliation, version conflicts, immutable electronic sign-off and printable signed prescriptions.
- Server-priced draft invoices, controlled issue, offline payment recording, idempotent payment references and printable receipts.
- Branch module settings with audit reasons; later-phase modules cannot be activated by a toggle.
- Responsive desktop/mobile navigation, patient search, dark/light appearance, role-aware views and honest unavailable-module states.

## Working Phase 2 slice

- Inpatient admission/transfer/discharge with a visual bed board, unique active admission/bed occupancy, cleaning/maintenance states and immutable discharge summaries.
- Nursing assessments, vitals, intake/output, pain/fall risk, configurable escalation alerts, inpatient medication orders, eMAR administrations with batch stock decrement and named shift handover acknowledgement.
- Pharmacy batch/expiry stock receipt, append-only stock movements, FEFO dispensing, OPD signed-prescription linkage, IPD active-medication-order linkage and request-hash idempotency.
- LIS test ordering, barcode/accession collection, processing, rejection, result entry, second-person technical validation, pathologist authorization, critical alerts and immutable authorized reports.
- Patient chart care-history tab with permission-filtered admissions, nursing notes, medication orders, dispenses and lab orders/results.
- Branch module settings now cover Phase 4; Phase 5+ modules still cannot be activated by a toggle.

## Working Phase 3 slice

- RIS workflow: imaging orders with modality/laterality validation, accession-numbered scheduling, study acquisition with DICOM Study Instance UIDs, structured reports with critical-result alerts and second-clinician authorization (authorizer ≠ reporter). **No live PACS/DICOM archive is connected** — the viewer-launch endpoint fails closed until an approved integration exists.
- Emergency board: walk-in/ambulance registration with linked-patient identity matching, triage categories (Red/Yellow/Green/Black), append-only observations and one-shot dispositions (Admit creates an admission and occupies a bed).
- Operation theatre: bookings with conflict-free theatre slots, the five-point surgical safety checklist required before time-in, server-clock procedure times and operation notes.
- Blood bank: unit receipt with unique unit numbers and component-aware expiry, screening quarantine, red-cell/plasma crossmatch compatibility rules, issue-to-transfusion/return closure with reaction alerts.
- Role switcher adds Radiologist and Blood Bank Technician.

The preview’s **Doctor** role is Dr. Anjali Sharma. The initial **Hospital Admin** role can manage registration, queues, billing and configuration but cannot sign clinical records. Use the role selector at the bottom of the sidebar to explore permissions. This selector exists only in Demo mode.

## Repository layout

```text
Careflow.slnx
src/Careflow.Domain/          clinical/financial records and invariants
src/Careflow.Application/     use cases, authorization scopes and persistence ports
src/Careflow.Infrastructure/  SQL adapter, identity hashing and explicit demo adapter
src/Careflow.Api/             REST API, session/CSRF checks, migration/provisioning commands
src/Careflow.Web/             React workspace, design system and browser tests
database/migrations/         versioned SQL Server migration
database/examples/           optional development configuration; not production master data
tests/Careflow.Tests/         executable application/domain tests
tests/Careflow.SqlTests/      SQL Server integration release gate (Phase 1 + Phase 2 data paths)
tests/api_smoke.py            loopback-only Phase 1 HTTP security/workflow checks
tests/care_smoke.py           loopback-only Phase 2 HTTP care workflow checks
docs/                        architecture, module contracts and runbooks
```

## Run the synthetic preview

Prerequisites: .NET 10 SDK and Node.js 22+. No SQL Server is needed for this explicitly development-only mode.

**Terminal 1 — API**

```bash
Careflow__Mode=Demo ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://0.0.0.0:5080 \
  dotnet run --project src/Careflow.Api
```

**Terminal 2 — workspace**

```bash
cd src/Careflow.Web
npm ci
npm run dev
```

Open `http://localhost:5173`. Vite proxies `/api` to the API; browser code never connects directly to a backend localhost URL. The preview binds to `0.0.0.0` for the Arena live preview and permits the proxied `.e2b.app` host. Demo data resets when the API restarts. The browser stores only the theme preference, not medical records.

Demo login accounts are `admin`, `doctor`, `reception`, `billing`, `nurse`, `nurse2`, `pharmacist`, `labtech`, `labtech2` and `pathologist`; the development password is `Preview!2026`. These accounts are never created by the SQL migration. Production refuses Demo mode when `ASPNETCORE_ENVIRONMENT=Production`.

## Run the full .NET Core stack (UI + API in one ASP.NET Core process)

The workspace bundle is served by the same ASP.NET Core host as the API (`wwwroot` + SPA fallback), so the deployed product is a single .NET process — React is compiled static content, not a separate runtime.

```bash
cd src/Careflow.Web
npm ci
npm run build          # writes src/Careflow.Web/dist
cd ../..
dotnet build           # copies the bundle into the API output as wwwroot
dotnet run --project src/Careflow.Api -- migrate   # SQL Server mode: apply migrations first
dotnet run --project src/Careflow.Api
```

Open `http://localhost:5080` — the hospital workspace and `/api/v1` both come from the .NET host. In `Careflow__Mode=Demo` the `migrate` step is skipped and the same single process serves the UI against the synthetic adapter. The Docker image (`Dockerfile`, default `api` stage) packages exactly this shape; `deploy/nginx.conf` remains an optional split edge (`--target workspace`).

### Restricted-network SDK build

The normal project references the official `Microsoft.Data.SqlClient` NuGet package. In this sandbox NuGet is unavailable, so compilation uses:

```bash
dotnet build -p:UseExternalSqlProvider=true
```

That property omits the package only for compilation; the provider-independent ADO.NET code still compiles. **It does not make SQL Server mode dependency-free.** A real SQL deployment must restore/package `Microsoft.Data.SqlClient` and its dependencies normally. Do not publish the external-provider override without supplying and testing the complete provider dependency set.

## SQL Server mode

The product is **.NET Core + SQL Server** end to end: ASP.NET Core `net10.0` minimal API, provider-independent ADO.NET stores (`Microsoft.Data.SqlClient` in real deployments) and versioned T-SQL migrations. `Careflow:Mode` defaults to `SqlServer`; Demo mode is an explicit, development-only opt-in that Production refuses. See the [deployment runbook](docs/DEPLOYMENT.md). In outline:

1. Provision SQL Server 2022+, a database and least-privilege deployment/application identities.
2. Set `ConnectionStrings__Careflow`, `Careflow__Mode=SqlServer`, `Careflow__HospitalId`, `Careflow__BranchId`, and the exact `Careflow__PublicOrigin`.
3. Run `dotnet run --project src/Careflow.Api -- migrate`.
4. Apply governed organization/catalog configuration. The optional example is development-only.
5. Use `provision-user` to create an administrator and qualified, practitioner-linked clinical users without hard-coded passwords.
6. Run the SQL integration release gate (`tests/Careflow.SqlTests`) against a disposable copy of the database, then complete the security, clinical and operational gates before deployment.

## Tests and verification

```bash
# Application/domain tests (40 checks)
dotnet run --project tests/Careflow.Tests

# SQL Server integration release gate (22 checks; skips without a connection string)
CAREFLOW_TEST_SQL="Server=...;Database=Careflow;..." dotnet run --project tests/Careflow.SqlTests

# Frontend unit tests and production bundle
cd src/Careflow.Web
npm test
npm run build

# With both demo processes running: HTTP security checks (21 checks)
python tests/api_smoke.py

# Phase 2 HTTP care workflow checks (20 checks)
python tests/care_smoke.py

# Browser tests (3 workflows); install a Playwright Chromium browser first
npx playwright install chromium
npm run test:e2e
```

For a preinstalled browser, set `CHROMIUM_EXECUTABLE_PATH`. Browser tests create synthetic records and are intended only for the loopback Demo API. Run HTTP smoke checks and browser workflows sequentially against a fresh demo instance; they share the same synthetic store. CI runs the SQL integration suite (`sql-integration` job) against a real SQL Server 2022 container: migrations apply through the API runner and every Phase 1/Phase 2 store flow executes on SQL Server. Static T-SQL parsing is used as an additional lint only and is never counted as a database integration test.

## Safety and scope

Basic allergy name matching is a development guard, **not** a comprehensive allergy/drug-interaction engine. Electronic sign-off is not a certificate-based digital signature. OPD investigation orders are recorded; LIS fulfillment is implemented as a separate laboratory worklist, while RIS fulfillment is not connected. Payments are recorded manually; no gateway charges a customer. Consent/documents/ABHA, patient merge, MFA/OIDC, schedule capacity templates, refunds/discount approvals, addenda and the remaining enterprise workflows are explicitly tracked in the Phase 1/Phase 2 status documents and roadmap.

No NABH/NABL/ABDM/DICOM certification or conformance is claimed. Production readiness requires the documented release gates, hospital-specific clinical governance, privacy review, real integration testing and operational validation.
