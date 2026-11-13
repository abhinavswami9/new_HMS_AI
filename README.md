# Careflow HIS

A modular hospital information system foundation: **ASP.NET Core / .NET 10, SQL Server, React + TypeScript**, with a responsive clinical workspace and an explicitly synthetic development preview.

> **Delivery status:** architecture + an initial Phase 1 vertical slice. This is not a certified, production-ready hospital platform. The live preview uses synthetic records and a development-only in-memory adapter. SQL Server schema/adapters are included but have not been executed against SQL Server in this sandbox. Do not enter real patient information into the preview.

## Start here

| Document                                               | Contents                                                                                                  |
| ------------------------------------------------------ | --------------------------------------------------------------------------------------------------------- |
| [System architecture](docs/ARCHITECTURE.md)            | modular architecture, tenancy, security, consistency, interoperability and operations                     |
| [Module specifications](docs/MODULE_SPECIFICATIONS.md) | every requested module: architecture, roles, tables, APIs, screens, rules, workflow, validation and audit |
| [Database architecture](docs/DATABASE.md)              | implemented schema, enterprise logical catalog, relationships, indexing and deployment controls           |
| [Roadmap](docs/ROADMAP.md)                             | five-phase delivery plan, current scope and production release gates                                      |
| [Phase 1 boundaries](docs/PHASE1_STATUS.md)            | exactly what works, what is partial and what remains planned                                              |
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
tests/api_smoke.py            loopback-only HTTP security/workflow checks
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

Demo login accounts are `admin`, `doctor`, `reception`, `billing`, and `nurse`; the development password is `Preview!2026`. These accounts are never created by the SQL migration. Production refuses Demo mode when `ASPNETCORE_ENVIRONMENT=Production`.

### Restricted-network SDK build

The normal project references the official `Microsoft.Data.SqlClient` NuGet package. In this sandbox NuGet is unavailable, so compilation uses:

```bash
dotnet build -p:UseExternalSqlProvider=true
```

That property omits the package only for compilation; the provider-independent ADO.NET code still compiles. **It does not make SQL Server mode dependency-free.** A real SQL deployment must restore/package `Microsoft.Data.SqlClient` and its dependencies normally. Do not publish the external-provider override without supplying and testing the complete provider dependency set.

## SQL Server mode

See the [deployment runbook](docs/DEPLOYMENT.md). In outline:

1. Provision SQL Server 2022+, a database and least-privilege deployment/application identities.
2. Set `ConnectionStrings__Careflow`, `Careflow__Mode=SqlServer`, `Careflow__HospitalId`, `Careflow__BranchId`, and the exact `Careflow__PublicOrigin`.
3. Run `dotnet run --project src/Careflow.Api -- migrate`.
4. Apply governed organization/catalog configuration. The optional example is development-only.
5. Use `provision-user` to create an administrator and qualified, practitioner-linked clinical users without hard-coded passwords.
6. Complete the SQL integration, security, clinical and operational gates before deployment.

## Tests and verification

```bash
# Application/domain tests (22 checks)
dotnet run --project tests/Careflow.Tests

# Frontend unit tests and production bundle
cd src/Careflow.Web
npm test
npm run build

# With both demo processes running: HTTP security checks (18 checks)
python tests/api_smoke.py

# Browser tests (2 workflows); install a Playwright Chromium browser first
npx playwright install chromium
npm run test:e2e
```

For a preinstalled browser, set `CHROMIUM_EXECUTABLE_PATH`. Browser tests create synthetic records and are intended only for the loopback Demo API. Run HTTP smoke checks and browser workflows sequentially against a fresh demo instance; they share the same synthetic store. SQL migration has also been statically parsed with the TSQL dialect; static parsing is **not** a SQL Server execution/integration test.

## Safety and scope

Basic allergy name matching is a development guard, **not** a comprehensive allergy/drug-interaction engine. Electronic sign-off is not a certificate-based digital signature. Orders are recorded but LIS/RIS fulfillment is not connected. Payments are recorded manually; no gateway charges a customer. Consent/documents/ABHA, patient merge, MFA/OIDC, schedule capacity templates, refunds/discount approvals, addenda and the remaining enterprise workflows are explicitly tracked in the Phase 1 status and roadmap.

No NABH/NABL/ABDM/DICOM certification or conformance is claimed. Production readiness requires the documented release gates, hospital-specific clinical governance, privacy review, real integration testing and operational validation.
