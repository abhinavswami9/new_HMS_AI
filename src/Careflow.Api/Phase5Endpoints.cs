using Careflow.Application;
using Careflow.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Careflow.Api;

// Phase 5 route groups: quality/accreditation, patient portal, management reporting.
public static class Phase5Endpoints
{
    public static void Map(RouteGroupBuilder root)
    {
        Careflow.Application.Scope S(HttpContext c) => Careflow.Application.Scope.For((Principal)c.Items["principal"]!, c.RequestServices.GetRequiredService<IClock>().UtcNow);

        var quality = root.MapGroup("/quality");
        quality.MapGet("/infections", async ([FromQuery] Guid? patientId, HttpContext c, QualityService s, CancellationToken ct) => Results.Ok(await s.ListInfectionsAsync(S(c), patientId, ct)));
        quality.MapPost("/infections", async (InfectionInput input, HttpContext c, QualityService s, CancellationToken ct) => Results.Ok(await s.ReportInfection(S(c), input, ct)));
        quality.MapPost("/infections/{id:guid}/investigate", async (Guid id, InfectionInvestigateInput input, HttpContext c, QualityService s, CancellationToken ct) => Results.Ok(await s.InvestigateInfection(S(c), id, input, ct)));
        quality.MapGet("/incidents", async (HttpContext c, QualityService s, CancellationToken ct) => Results.Ok(await s.ListIncidentsAsync(S(c), ct)));
        quality.MapPost("/incidents", async (IncidentInput input, HttpContext c, QualityService s, CancellationToken ct) => Results.Ok(await s.ReportIncident(S(c), input, ct)));
        quality.MapPost("/incidents/{id:guid}/triage", async (Guid id, IncidentTriageInput input, HttpContext c, QualityService s, CancellationToken ct) => Results.Ok(await s.TriageIncident(S(c), id, input, ct)));
        quality.MapPost("/incidents/{id:guid}/close", async (Guid id, IncidentCloseInput input, HttpContext c, QualityService s, CancellationToken ct) => Results.Ok(await s.CloseIncident(S(c), id, input, ct)));
        quality.MapGet("/risks", async (HttpContext c, QualityService s, CancellationToken ct) => Results.Ok(await s.ListRisksAsync(S(c), ct)));
        quality.MapPost("/risks", async (RiskInput input, HttpContext c, QualityService s, CancellationToken ct) => Results.Ok(await s.AssessRisk(S(c), input, ct)));
        quality.MapPost("/risks/{id:guid}/update", async (Guid id, RiskUpdateInput input, HttpContext c, QualityService s, CancellationToken ct) => Results.Ok(await s.UpdateRisk(S(c), id, input, ct)));
        quality.MapGet("/capa", async (HttpContext c, QualityService s, CancellationToken ct) => Results.Ok(await s.ListCapasAsync(S(c), ct)));
        quality.MapPost("/capa", async (CapaInput input, HttpContext c, QualityService s, CancellationToken ct) => Results.Ok(await s.OpenCapa(S(c), input, ct)));
        quality.MapPost("/capa/{id:guid}/progress", async (Guid id, CapaProgressInput input, HttpContext c, QualityService s, CancellationToken ct) => Results.Ok(await s.ProgressCapa(S(c), id, input, ct)));
        quality.MapPost("/capa/{id:guid}/verify", async (Guid id, CapaVerifyInput input, HttpContext c, QualityService s, CancellationToken ct) => Results.Ok(await s.VerifyCapa(S(c), id, input, ct)));
        quality.MapPost("/capa/{id:guid}/close", async (Guid id, CapaCloseInput input, HttpContext c, QualityService s, CancellationToken ct) => Results.Ok(await s.CloseCapa(S(c), id, input, ct)));
        quality.MapGet("/audits", async (HttpContext c, QualityService s, CancellationToken ct) => Results.Ok(await s.ListAuditsAsync(S(c), ct)));
        quality.MapPost("/audits", async (AuditInput input, HttpContext c, QualityService s, CancellationToken ct) => Results.Ok(await s.PlanAudit(S(c), input, ct)));
        quality.MapPost("/audits/{id:guid}/findings", async (Guid id, AuditFindingsInput input, HttpContext c, QualityService s, CancellationToken ct) => Results.Ok(await s.RecordAuditFindings(S(c), id, input, ct)));
        quality.MapPost("/audits/{id:guid}/report", async (Guid id, AuditReportInput input, HttpContext c, QualityService s, CancellationToken ct) => Results.Ok(await s.ReportAudit(S(c), id, input, ct)));
        quality.MapGet("/indicators", async (HttpContext c, QualityService s, CancellationToken ct) => Results.Ok(await s.ListIndicatorsAsync(S(c), ct)));
        quality.MapGet("/indicators/{id:guid}/results", async (Guid id, HttpContext c, QualityService s, CancellationToken ct) => Results.Ok(await s.ListIndicatorResultsAsync(S(c), id, ct)));
        quality.MapPost("/indicators", async (QualityIndicatorInput input, HttpContext c, QualityService s, CancellationToken ct) => Results.Ok(await s.DefineIndicator(S(c), input, ct)));
        quality.MapPost("/indicators/{id:guid}/results", async (Guid id, QualityIndicatorResultInput input, HttpContext c, QualityService s, CancellationToken ct) => Results.Ok(await s.RecordIndicatorResult(S(c), id, input, ct)));
        quality.MapGet("/documents", async (HttpContext c, QualityService s, CancellationToken ct) => Results.Ok(await s.ListDocumentsAsync(S(c), ct)));
        quality.MapPost("/documents", async (DocumentInput input, HttpContext c, QualityService s, CancellationToken ct) => Results.Ok(await s.CreateDocument(S(c), input, ct)));
        quality.MapGet("/documents/{id:guid}/versions", async (Guid id, HttpContext c, QualityService s, CancellationToken ct) => Results.Ok(await s.ListDocumentVersionsAsync(S(c), id, ct)));
        quality.MapGet("/documents/{id:guid}/acknowledgments", async (Guid id, HttpContext c, QualityService s, CancellationToken ct) => Results.Ok(await s.ListAcknowledgmentsAsync(S(c), id, ct)));
        quality.MapPost("/documents/{id:guid}/versions", async (Guid id, DocumentVersionInput input, HttpContext c, QualityService s, CancellationToken ct) => Results.Ok(await s.NewDocumentVersion(S(c), id, input, ct)));
        quality.MapPost("/documents/{id:guid}/submit", async (Guid id, DocumentTransitionInput input, HttpContext c, QualityService s, CancellationToken ct) => Results.Ok(await s.SubmitDocument(S(c), id, input, ct)));
        quality.MapPost("/documents/{id:guid}/approve", async (Guid id, DocumentTransitionInput input, HttpContext c, QualityService s, CancellationToken ct) => Results.Ok(await s.ApproveDocument(S(c), id, input, ct)));
        quality.MapPost("/documents/{id:guid}/publish", async (Guid id, DocumentTransitionInput input, HttpContext c, QualityService s, CancellationToken ct) => Results.Ok(await s.PublishDocument(S(c), id, input, ct)));
        quality.MapPost("/documents/{id:guid}/retire", async (Guid id, DocumentTransitionInput input, HttpContext c, QualityService s, CancellationToken ct) => Results.Ok(await s.RetireDocument(S(c), id, input, ct)));
        quality.MapPost("/documents/{id:guid}/acknowledge", async (Guid id, DocumentAcknowledgeInput input, HttpContext c, QualityService s, CancellationToken ct) => Results.Ok(await s.AcknowledgeDocument(S(c), id, input, ct)));

        var portal = root.MapGroup("/portal");
        portal.MapPost("/accounts", async (PortalRegisterInput input, HttpContext c, PortalService s, CancellationToken ct) => Results.Ok(await s.Register(S(c), input, ct)));
        portal.MapPost("/accounts/{id:guid}/verify", async (Guid id, PortalVerifyInput input, HttpContext c, PortalService s, CancellationToken ct) => Results.Ok(await s.VerifyAccount(S(c), id, input, ct)));
        portal.MapGet("/profile", async (HttpContext c, PortalService s, CancellationToken ct) => Results.Ok(await s.GetProfile(S(c), ct)));
        portal.MapPost("/login", async (PortalLoginInput input, HttpContext c, PortalService s, CancellationToken ct) => Results.Ok(await s.Login(S(c), input, ct)));
        portal.MapPost("/preferences", async (PortalPreferenceInput input, HttpContext c, PortalService s, CancellationToken ct) => Results.Ok(await s.UpdatePreferences(S(c), input, ct)));
        portal.MapPost("/links", async (PortalLinkInput input, HttpContext c, PortalService s, CancellationToken ct) => Results.Ok(await s.LinkPatient(S(c), input, ct)));
        portal.MapPost("/patients/{patientId:guid}/proxy", async (Guid patientId, ProxyGrantInput input, HttpContext c, PortalService s, CancellationToken ct) => Results.Ok(await s.GrantProxy(S(c), patientId, input, ct)));
        portal.MapPost("/proxy/{id:guid}/revoke", async (Guid id, ProxyRevokeInput input, HttpContext c, PortalService s, CancellationToken ct) => Results.Ok(await s.RevokeProxy(S(c), id, input, ct)));
        portal.MapGet("/appointments", async (Guid? patientId, HttpContext c, PortalService s, CancellationToken ct) => Results.Ok(await s.GetAppointments(S(c), patientId, ct)));
        portal.MapPost("/appointments", async (PortalAppointmentInput input, HttpContext c, PortalService s, CancellationToken ct) => Results.Ok(await s.BookAppointment(S(c), input, ct)));
        portal.MapGet("/prescriptions", async (Guid? patientId, HttpContext c, PortalService s, CancellationToken ct) => Results.Ok(await s.GetPrescriptions(S(c), patientId, ct)));
        portal.MapGet("/summaries", async (Guid? patientId, HttpContext c, PortalService s, CancellationToken ct) => Results.Ok(await s.GetSummaries(S(c), patientId, ct)));
        portal.MapGet("/reports", async (Guid? patientId, HttpContext c, PortalService s, CancellationToken ct) => Results.Ok(await s.GetReports(S(c), patientId, ct)));
        portal.MapGet("/bills", async (Guid? patientId, HttpContext c, PortalService s, CancellationToken ct) => Results.Ok(await s.GetBills(S(c), patientId, ct)));
        portal.MapPost("/payments", async (PortalPaymentInput input, HttpContext c, PortalService s, CancellationToken ct) => Results.Ok(await s.Pay(S(c), input, ct)));
        portal.MapPost("/consents", async (PortalConsentInput input, HttpContext c, PortalService s, CancellationToken ct) => Results.Ok(await s.RecordConsent(S(c), input, ct)));
        portal.MapGet("/documents/{id:guid}/download", async (Guid id, [FromQuery] string kind, [FromQuery] Guid? patientId, HttpContext c, PortalService s, CancellationToken ct) => Results.Ok(await s.Download(S(c), id, kind, patientId, ct)));

        var reports = root.MapGroup("/reports");
        reports.MapGet("/definitions", async (HttpContext c, ReportingService s, CancellationToken ct) => Results.Ok(await s.ListDefinitionsAsync(S(c), ct)));
        reports.MapPost("/definitions", async (ReportDefinitionInput input, HttpContext c, ReportingService s, CancellationToken ct) => Results.Ok(await s.Define(S(c), input, ct)));
        reports.MapPost("/definitions/{id:guid}/approve", async (Guid id, VersionInput input, HttpContext c, ReportingService s, CancellationToken ct) => Results.Ok(await s.ApproveDefinition(S(c), id, input, ct)));
        reports.MapGet("/executions", async (HttpContext c, ReportingService s, CancellationToken ct) => Results.Ok(await s.ListExecutionsAsync(S(c), ct)));
        reports.MapPost("/execute", async (ReportExecuteInput input, HttpContext c, ReportingService s, CancellationToken ct) => Results.Ok(await s.Execute(S(c), input, ct)));
        reports.MapGet("/executions/{id:guid}", async (Guid id, HttpContext c, ReportingService s, CancellationToken ct) => Results.Ok(await s.GetExecution(S(c), id, ct)));
        reports.MapGet("/exports", async (HttpContext c, ReportingService s, CancellationToken ct) => Results.Ok(await s.ListExportsAsync(S(c), ct)));
        reports.MapPost("/exports", async (ReportExportInput input, HttpContext c, ReportingService s, CancellationToken ct) => Results.Ok(await s.Export(S(c), input, ct)));
        reports.MapPost("/subscriptions", async (SubscriptionInput input, HttpContext c, ReportingService s, CancellationToken ct) => Results.Ok(await s.Subscribe(S(c), input, ct)));
        reports.MapGet("/dashboards/{view}", async (string view, HttpContext c, ReportingService s, CancellationToken ct) => Results.Ok(await s.Dashboard(S(c), view, ct)));
    }
}
