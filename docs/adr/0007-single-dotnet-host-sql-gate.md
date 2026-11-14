# ADR-0007: Single ASP.NET Core host for UI + API, SQL Server as the release gate

Status: accepted.

Decision: the product is a .NET Core system end to end. One ASP.NET Core `net10.0` process serves the compiled React workspace (`wwwroot` with SPA fallback and the deployment CSP) and the REST API, so the deployed shape is a single .NET application; React ships as compiled static content, not a separate runtime. SQL Server is the system of record: `Careflow:Mode` defaults to `SqlServer`, the Demo adapter stays an explicit development-only opt-in that Production refuses, and the Phase 1/Phase 2 data paths run as a dedicated SQL Server integration suite (`tests/Careflow.SqlTests`) in a CI job with a real SQL Server container. Static T-SQL parsing is lint only and is never labelled an integration test.

Consequences: `npm run build` before `dotnet build` embeds the workspace bundle in the API output and in `dotnet publish`/the Docker image (default `api` stage). The nginx configuration remains an optional split edge (`--target workspace`). The sandbox preview still runs the Demo adapter because no SQL Server engine exists there; CI executes migrations and every store flow against SQL Server, and release requires that job to pass alongside the application, HTTP and browser suites.
