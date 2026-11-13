# Deployment and provisioning runbook

## 1. Preconditions

This runbook is for a qualified deployment team. The repository contains Phase 1 and Phase 2 slices, not a release candidate. Complete PHASE1_STATUS.md, PHASE2_STATUS.md and ROADMAP.md gates before handling real patient data.

- .NET 10 SDK on the build host; supported runtime on the server.
- SQL Server 2022+ with encrypted connections and backups.
- Normal access to the official NuGet feed for `Microsoft.Data.SqlClient` 6.1.8 and its transitive dependencies.
- Node.js 22+ for the frontend build; self-hosted fonts/assets.
- HTTPS reverse proxy, exact public origin, secret manager, private object storage and monitoring planned.

Never use `UseExternalSqlProvider=true` for a normal production publish. That property exists to compile provider-independent code in a network-restricted development sandbox; it omits the runtime SQL package.

## 2. Database and identities

Create a database (for example `Careflow`) and separate deployment, application and reporting identities. The migration does not create logins/users or grant permissions. Apply least-privilege grants after schema creation:

- Application: SELECT/INSERT/UPDATE on required tables; no DELETE on audit, signed clinical or financial records; no DDL.
- Deployment: migration DDL and migration-history write access, used only during releases.
- Audit: application INSERT/SELECT; no UPDATE/DELETE. Configure immutable export/WORM storage separately.
- Reporting: scoped/redacted read views, not unrestricted clinical table access.

Enable encryption at rest, backup encryption and a tested restore process. Consider READ_COMMITTED_SNAPSHOT only after reviewing the unit-of-work isolation strategy; the initial adapter uses serializable transactions for correctness, not a tuned high-concurrency workload.

## 3. Configuration

Supply secrets through environment/secret manager, never source control:

```text
ASPNETCORE_ENVIRONMENT=Production
Careflow__Mode=SqlServer
ConnectionStrings__Careflow=<secret SQL connection string; Encrypt=True; trusted certificate>
Careflow__HospitalId=<existing hospital GUID>
Careflow__BranchId=<existing branch GUID>
Careflow__PublicOrigin=https://hospital.example.org
Careflow__TrustedProxies=<comma-separated proxy IPs/CIDRs; defaults to loopback>
ASPNETCORE_URLS=http://0.0.0.0:8080
```

The reverse proxy terminates TLS and forwards to the API. Configure `Careflow:TrustedProxies` with the exact proxy IPs/CIDRs for your network; the default trusts loopback only. Do not blindly trust arbitrary X-Forwarded-* headers. Permit only the exact public origin for cookie-authenticated writes. Keep SQL Server private.

## 4. Apply migrations

Build/restore normally, then:

```bash
dotnet run --project src/Careflow.Api -- migrate
```

The runner reads `ConnectionStrings__Careflow`, creates `dbo.SchemaMigrations` if needed, executes unapplied migration batches in a transaction and records a SHA-256 checksum. An already-applied migration with changed content is rejected. Correct mistakes with a new forward migration; do not edit applied history.

Migrations 0001 and 0002 create schema and global permission/module catalog only. Migration 0002 also seeds an unapproved `nursing.escalation` policy per branch; approve it through the hospital policy process before enabling nursing observations. The migrations do not insert patients, practitioners, prices, medicines, beds, stock, lab tests or usable credentials.

## 5. Organization and catalog

Apply approved organization/branch/department/module/practitioner/service/price/formulary configuration using your governed deployment process. For a disposable development database only, `database/examples/phase1_configuration.sql` provides a sample hospital and catalog, and `database/examples/phase2_configuration.sql` enables Phase 2 modules with sample beds and lab tests. They are install-once and are not an approved clinical formulary, price list, bed master or reference-interval catalog.

The example IDs are:

```text
Hospital: 10000000-0000-0000-0000-000000000001
Branch:   20000000-0000-0000-0000-000000000001
Doctor:   40000000-0000-0000-0000-000000000001
```

Replace these with your organization’s identifiers in a real deployment. Configure effective prices before enabling billing. License and validate terminology/formulary content.

## 6. Provision users without hard-coded passwords

Set `CAREFLOW_ADMIN_USERNAME`, `CAREFLOW_ADMIN_NAME`, `CAREFLOW_ADMIN_PASSWORD`, `CAREFLOW_HOSPITAL_ID`, and `CAREFLOW_BRANCH_ID` through a secure secret-injection mechanism. The variable prefix is historical; the command provisions the role in `CAREFLOW_PROVISION_ROLE` (default `Hospital Admin`). Passwords require at least 14 characters with letters, digits and a symbol. Do not put secret values in shell history or command arguments.

```bash
dotnet run --project src/Careflow.Api -- provision-user
```

For a doctor, set `CAREFLOW_PROVISION_ROLE=Doctor` and `CAREFLOW_PRACTITIONER_ID` to an existing, unlinked practitioner. The command links the qualified practitioner to the user and grants the default doctor permissions. Other default roles are Receptionist, Billing Staff, Nurse, Pharmacist, Lab Technician and Pathologist. Existing usernames are rejected rather than silently reset. Clear injected password secrets from the deployment environment after use.

Administrative permission does not grant clinical signing. Enterprise MFA/OIDC and credential lifecycle workflows are still required before remote production access.

## 7. Frontend and proxy

```bash
cd src/Careflow.Web
npm ci
npm run build
```

Container images are provided but were not built in the sandbox (no Docker daemon):

```bash
docker build -t careflow-api .
docker build --target workspace -t careflow-workspace .
```

Run the API and workspace on a private Docker network with the API reachable as `api:8080`, or serve the generated static bundle through your HTTPS edge and proxy `/api/` and `/health/` to the API. Preserve the public Host and configure the exact origin. Do not expose the Vite development server in production. Browser API calls are same-origin relative URLs. Session cookies are Secure in SQL/production mode. Apply an edge CSP appropriate to the built assets; do not copy the development host allowlist into production.

## 8. Verification before traffic

1. Run migration against a disposable SQL instance and verify all constraints/triggers.
2. Test login, MFA integration, revocation, CSRF/origin and tenant/branch negative cases.
3. Register two distinct patients and a duplicate candidate; verify UHID/audit behavior.
4. Book overlapping slots concurrently; verify one allocation and no duplicate tokens.
5. Save/sign an encounter; verify immutable content, child records, versions and audit/outbox commit together.
6. Issue/pay an invoice; retry the same reference concurrently; verify one payment and correct balance.
7. Attempt direct updates/deletes of signed records, payments and audit using the application identity; verify denial.
8. Verify private downloads, backup restore, audit export, monitoring and downtime procedures.
9. Complete clinical, financial, privacy, accessibility and load/soak sign-off.

## 9. Known operational gaps

The outbox is persisted but has no dispatcher yet. Notification/PACS/ABDM/payment adapters are not connected. SQL integration and performance tests remain open. The initial workspace is bounded; complete archive/report pagination is required at scale. Do not interpret a successful health check as completion of these gates.
