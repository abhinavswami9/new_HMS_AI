using Careflow.Domain;
namespace Careflow.Application;

public sealed class QualityService(IUnitOfWork unitOfWork, IClock clock)
{
    static void Guard(bool ok, string code, string message, int status = 422) { if (!ok) throw new DomainException(code, message, status); }
    static readonly string[] InfectionCategories = ["Healthcare-associated", "Community-associated", "Device-associated", "Surgical site", "Bloodstream", "Urinary", "Respiratory"];
    static readonly string[] Severities = ["Mild", "Moderate", "Severe", "Life-threatening"];
    static readonly string[] IncidentCategories = ["Medication", "Fall", "Documentation", "Equipment", "Infection control", "Near miss", "Security", "Other"];
    static readonly string[] RiskDomains = ["Clinical", "Operational", "Financial", "Infection control", "Safety", "Information security"];
    static readonly string[] DocCategories = ["SOP", "Policy", "Guideline", "Form", "Plan"];

    public Task<Infection> ReportInfection(Scope scope, InfectionInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.QualityInfection); await CareGuard.Module(s, "quality", ct);
        _ = await s.FindPatientAsync(input.PatientId, ct) ?? throw new DomainException("not_found", "Patient not found.", 404);
        Guard(InfectionCategories.Contains(input.Category), "quality.fields", "Choose a valid infection category.");
        var site = CareGuard.Text(input.Site, 2, 120, "Infection site");
        var method = CareGuard.Text(input.DetectedMethod, 2, 120, "Detection method");
        Guard(Severities.Contains(input.Severity), "quality.fields", "Choose a valid severity.");
        Guard(input.DetectedOn <= DateOnly.FromDateTime(clock.UtcNow.UtcDateTime), "quality.fields", "Detection date cannot be in the future.");
        var number = await s.NextNumberAsync("INF", $"{scope.BranchId}:{clock.UtcNow.Year}", $"INF-{clock.UtcNow.Year}", ct);
        var infection = new Infection(Guid.NewGuid(), number, input.PatientId, input.EncounterId, input.Category,
            site, method, input.DetectedOn, input.Severity, "Reported", scope.Actor.Name, clock.UtcNow, "", "");
        await s.InsertInfectionAsync(infection, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "quality.infection.reported", "Infection", infection.Id, changes: new { input.Category, input.Severity }), ct);
        return infection;
    }, ct);

    public Task<Infection> InvestigateInfection(Scope scope, Guid id, InfectionInvestigateInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.QualityInfection); await CareGuard.Module(s, "quality", ct);
        var infection = await s.FindInfectionAsync(id, ct) ?? throw new DomainException("not_found", "Infection record not found.", 404);
        CareGuard.Version(infection.Version, input.Version);
        Guard(infection.Status is "Reported" or "Confirmed" or "Rejected", "quality.state", "Only open infection records can be updated.");
        Guard(input.Status is "Confirmed" or "Rejected" or "Closed", "quality.fields", "Status must be Confirmed, Rejected or Closed.");
        var investigation = CareGuard.Text(input.Investigation, 5, 2000, "Investigation");
        var outcome = CareGuard.Text(input.Outcome, 0, 1000, "Outcome");
        var updated = infection with { Investigation = investigation, Outcome = outcome, Status = input.Status, Version = infection.Version + 1 };
        await s.UpdateInfectionAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "quality.infection.updated", "Infection", id, input.Status, new { updated.Severity }), ct);
        return updated;
    }, ct);

    public Task<Incident> ReportIncident(Scope scope, IncidentInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.QualityIncident); await CareGuard.Module(s, "quality", ct);
        Guard(IncidentCategories.Contains(input.Category), "quality.fields", "Choose a valid incident category.");
        Guard(Severities.Contains(input.Severity), "quality.fields", "Choose a valid severity.");
        var description = CareGuard.Text(input.Description, 10, 4000, "Incident description");
        var location = CareGuard.Text(input.Location, 2, 200, "Location");
        if (input.PatientId is Guid pid) _ = await s.FindPatientAsync(pid, ct) ?? throw new DomainException("not_found", "Patient not found.", 404);
        var number = await s.NextNumberAsync("QIR", $"{scope.BranchId}:{clock.UtcNow.Year}", $"QIR-{clock.UtcNow.Year}", ct);
        var incident = new Incident(Guid.NewGuid(), number, input.Category, input.Severity, description, location,
            input.PatientId, scope.Actor.Name, clock.UtcNow, "Reported", "", "", null, "", "");
        await s.InsertIncidentAsync(incident, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "quality.incident.reported", "Incident", incident.Id, changes: new { input.Category, input.Severity }), ct);
        return incident;
    }, ct);

    public Task<Incident> TriageIncident(Scope scope, Guid id, IncidentTriageInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.QualityCapa); await CareGuard.Module(s, "quality", ct);
        var incident = await s.FindIncidentAsync(id, ct) ?? throw new DomainException("not_found", "Incident not found.", 404);
        CareGuard.Version(incident.Version, input.Version);
        Guard(incident.Status == "Reported", "quality.state", "Only reported incidents can be triaged.");
        var note = CareGuard.Text(input.TriageNote, 5, 2000, "Triage note");
        var owner = CareGuard.Text(input.AssignedTo, 2, 120, "Assigned owner");
        var updated = incident with { Status = "UnderInvestigation", TriageNote = note, AssignedTo = owner, TriagedAt = clock.UtcNow, Version = incident.Version + 1 };
        await s.UpdateIncidentAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "quality.incident.triaged", "Incident", id, note, new { owner }), ct);
        return updated;
    }, ct);

    public Task<Incident> CloseIncident(Scope scope, Guid id, IncidentCloseInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.QualityCapa); await CareGuard.Module(s, "quality", ct);
        var incident = await s.FindIncidentAsync(id, ct) ?? throw new DomainException("not_found", "Incident not found.", 404);
        CareGuard.Version(incident.Version, input.Version);
        Guard(incident.Status is "UnderInvestigation" or "CapaAssigned", "quality.state", "Only investigated incidents can be closed.");
        var openCapa = (await s.ListCapasAsync(ct)).Any(c => c.IncidentId == id && c.Status is not ("Closed" or "Cancelled"));
        Guard(!openCapa, "quality.completeness", "Close linked CAPA actions before closing this incident.");
        var investigation = CareGuard.Text(input.Investigation, 5, 4000, "Investigation");
        var rootCause = CareGuard.Text(input.RootCause, 5, 2000, "Root cause");
        var updated = incident with { Status = "Closed", Investigation = investigation, RootCause = rootCause, Version = incident.Version + 1 };
        await s.UpdateIncidentAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "quality.incident.closed", "Incident", id, rootCause), ct);
        return updated;
    }, ct);

    public Task<RiskAssessment> AssessRisk(Scope scope, RiskInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.QualityRisk); await CareGuard.Module(s, "quality", ct);
        Guard(RiskDomains.Contains(input.Domain), "quality.fields", "Choose a valid risk domain.");
        var description = CareGuard.Text(input.Description, 5, 2000, "Risk description");
        Guard(input.Probability is >= 1 and <= 5 && input.Impact is >= 1 and <= 5, "quality.fields", "Probability and impact must be between 1 and 5.");
        var mitigation = CareGuard.Text(input.Mitigation, 5, 2000, "Mitigation");
        var owner = CareGuard.Text(input.Owner, 2, 120, "Risk owner");
        var number = await s.NextNumberAsync("RSK", $"{scope.BranchId}:{clock.UtcNow.Year}", $"RSK-{clock.UtcNow.Year}", ct);
        var risk = new RiskAssessment(Guid.NewGuid(), number, input.Domain, description, input.Probability,
            input.Impact, mitigation, owner, "Open", clock.UtcNow);
        await s.InsertRiskAsync(risk, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "quality.risk.assessed", "RiskAssessment", risk.Id, changes: new { input.Probability, input.Impact, risk.Score }), ct);
        return risk;
    }, ct);

    public Task<RiskAssessment> UpdateRisk(Scope scope, Guid id, RiskUpdateInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.QualityRisk); await CareGuard.Module(s, "quality", ct);
        var risk = await s.FindRiskAsync(id, ct) ?? throw new DomainException("not_found", "Risk not found.", 404);
        CareGuard.Version(risk.Version, input.Version);
        Guard(risk.Status is not "Closed", "quality.state", "Closed risks cannot be updated.");
        Guard(input.Status is "Open" or "Mitigating" or "Closed", "quality.fields", "Status must be Open, Mitigating or Closed.");
        var mitigation = CareGuard.Text(input.Mitigation, 5, 2000, "Mitigation");
        var owner = CareGuard.Text(input.Owner, 2, 120, "Risk owner");
        var updated = risk with { Mitigation = mitigation, Owner = owner, Status = input.Status, Version = risk.Version + 1 };
        await s.UpdateRiskAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "quality.risk.updated", "RiskAssessment", id, input.Status, new { updated.Score }), ct);
        return updated;
    }, ct);

    public Task<Capa> OpenCapa(Scope scope, CapaInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.QualityCapa); await CareGuard.Module(s, "quality", ct);
        Guard(input.IncidentId is not null || input.InfectionId is not null || input.AuditId is not null || input.RiskId is not null,
            "quality.fields", "A CAPA must reference an incident, infection, audit or risk.");
        if (input.IncidentId is Guid iid) _ = await s.FindIncidentAsync(iid, ct) ?? throw new DomainException("not_found", "Referenced incident not found.", 404);
        if (input.InfectionId is Guid ifid) _ = await s.FindInfectionAsync(ifid, ct) ?? throw new DomainException("not_found", "Referenced infection not found.", 404);
        if (input.AuditId is Guid aid) _ = await s.FindAuditAsync(aid, ct) ?? throw new DomainException("not_found", "Referenced audit not found.", 404);
        if (input.RiskId is Guid rid) _ = await s.FindRiskAsync(rid, ct) ?? throw new DomainException("not_found", "Referenced risk not found.", 404);
        var correction = CareGuard.Text(input.Correction, 5, 2000, "Correction");
        var corrective = CareGuard.Text(input.CorrectiveAction, 5, 2000, "Corrective action");
        var preventive = CareGuard.Text(input.PreventiveAction, 5, 2000, "Preventive action");
        var owner = CareGuard.Text(input.Owner, 2, 120, "CAPA owner");
        Guard(input.DueDate > clock.UtcNow, "quality.fields", "CAPA due date must be in the future.");
        var number = await s.NextNumberAsync("CAPA", $"{scope.BranchId}:{clock.UtcNow.Year}", $"CAPA-{clock.UtcNow.Year}", ct);
        var capa = new Capa(Guid.NewGuid(), number, input.IncidentId, input.InfectionId, input.AuditId, input.RiskId,
            correction, corrective, preventive, owner, input.DueDate, "Open", "", false, "", null, clock.UtcNow, scope.Actor.Name);
        await s.InsertCapaAsync(capa, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "quality.capa.opened", "Capa", capa.Id, correction, new { owner }), ct);
        return capa;
    }, ct);

    public Task<Capa> ProgressCapa(Scope scope, Guid id, CapaProgressInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.QualityCapa); await CareGuard.Module(s, "quality", ct);
        var capa = await s.FindCapaAsync(id, ct) ?? throw new DomainException("not_found", "CAPA not found.", 404);
        CareGuard.Version(capa.Version, input.Version);
        Guard(capa.Status is "Open" or "InProgress", "quality.state", "Only open CAPA actions can be updated.");
        var corrective = CareGuard.Text(input.CorrectiveAction, 5, 2000, "Corrective action");
        var preventive = CareGuard.Text(input.PreventiveAction, 5, 2000, "Preventive action");
        var updated = capa with { CorrectiveAction = corrective, PreventiveAction = preventive, Status = "InProgress", Version = capa.Version + 1 };
        await s.UpdateCapaAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "quality.capa.progressed", "Capa", id, changes: new { updated.Status }), ct);
        return updated;
    }, ct);

    public Task<Capa> VerifyCapa(Scope scope, Guid id, CapaVerifyInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.QualityCapa); await CareGuard.Module(s, "quality", ct);
        var capa = await s.FindCapaAsync(id, ct) ?? throw new DomainException("not_found", "CAPA not found.", 404);
        CareGuard.Version(capa.Version, input.Version);
        Guard(capa.Status is "Open" or "InProgress", "quality.state", "Only open CAPA actions can be verified.");
        var evidence = CareGuard.Text(input.EffectivenessEvidence, 10, 4000, "Effectiveness evidence");
        var updated = capa with { EffectivenessEvidence = evidence, EffectivenessVerified = true, VerifiedBy = scope.Actor.Name, VerifiedAt = clock.UtcNow, Status = "Verified", Version = capa.Version + 1 };
        await s.UpdateCapaAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "quality.capa.verified", "Capa", id, evidence), ct);
        return updated;
    }, ct);

    public Task<Capa> CloseCapa(Scope scope, Guid id, CapaCloseInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.QualityCapa); await CareGuard.Module(s, "quality", ct);
        var capa = await s.FindCapaAsync(id, ct) ?? throw new DomainException("not_found", "CAPA not found.", 404);
        CareGuard.Version(capa.Version, input.Version);
        Guard(capa.Status == "Verified", "quality.state", "Verify effectiveness before closing a CAPA.");
        _ = CareGuard.Text(input.Reason, 5, 2000, "Closure reason");
        var updated = capa with { Status = "Closed", Version = capa.Version + 1 };
        await s.UpdateCapaAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "quality.capa.closed", "Capa", id, input.Reason), ct);
        return updated;
    }, ct);

    public Task<ClinicalAudit> PlanAudit(Scope scope, AuditInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.QualityAudit); await CareGuard.Module(s, "quality", ct);
        var domain = CareGuard.Text(input.Domain, 2, 200, "Audit domain");
        var scopeText = CareGuard.Text(input.Scope, 5, 2000, "Audit scope");
        var lead = CareGuard.Text(input.Lead, 2, 120, "Audit lead");
        var number = await s.NextNumberAsync("AUD", $"{scope.BranchId}:{clock.UtcNow.Year}", $"AUD-{clock.UtcNow.Year}", ct);
        var audit = new ClinicalAudit(Guid.NewGuid(), number, domain, scopeText, input.PlannedFor, lead,
            "Planned", "", 0, "", clock.UtcNow, scope.Actor.Name);
        await s.InsertAuditAsync(audit, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "quality.audit.planned", "ClinicalAudit", audit.Id, changes: new { domain, input.PlannedFor }), ct);
        return audit;
    }, ct);

    public Task<ClinicalAudit> RecordAuditFindings(Scope scope, Guid id, AuditFindingsInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.QualityAudit); await CareGuard.Module(s, "quality", ct);
        var audit = await s.FindAuditAsync(id, ct) ?? throw new DomainException("not_found", "Audit not found.", 404);
        CareGuard.Version(audit.Version, input.Version);
        Guard(audit.Status == "Planned", "quality.state", "Findings can only be recorded against planned audits.");
        var findings = CareGuard.Text(input.Findings, 5, 8000, "Findings");
        Guard(input.ScorePercent is >= 0 and <= 100, "quality.fields", "Score must be between 0 and 100.");
        var updated = audit with { Status = "InProgress", Findings = findings, ScorePercent = input.ScorePercent, Version = audit.Version + 1 };
        await s.UpdateAuditAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "quality.audit.findings", "ClinicalAudit", id, changes: new { input.ScorePercent }), ct);
        return updated;
    }, ct);

    public Task<ClinicalAudit> ReportAudit(Scope scope, Guid id, AuditReportInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.QualityAudit); await CareGuard.Module(s, "quality", ct);
        var audit = await s.FindAuditAsync(id, ct) ?? throw new DomainException("not_found", "Audit not found.", 404);
        CareGuard.Version(audit.Version, input.Version);
        Guard(audit.Status == "InProgress", "quality.state", "Only audits with findings can be reported.");
        var report = CareGuard.Text(input.Report, 5, 8000, "Audit report");
        var updated = audit with { Status = "Closed", Report = report, Version = audit.Version + 1 };
        await s.UpdateAuditAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "quality.audit.closed", "ClinicalAudit", id, changes: new { updated.ScorePercent }), ct);
        return updated;
    }, ct);

    public Task<QualityIndicator> DefineIndicator(Scope scope, QualityIndicatorInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.QualityIndicator); await CareGuard.Module(s, "quality", ct);
        var code = CareGuard.Text(input.Code, 2, 20, "Indicator code").ToUpperInvariant();
        Guard((await s.FindIndicatorByCodeAsync(code, ct)) is null, "quality.duplicate", "An indicator with this code already exists.");
        var name = CareGuard.Text(input.Name, 3, 200, "Indicator name");
        var definition = CareGuard.Text(input.Definition, 5, 2000, "Indicator definition");
        var numerator = CareGuard.Text(input.Numerator, 2, 500, "Numerator definition");
        var denominator = CareGuard.Text(input.Denominator, 2, 500, "Denominator definition");
        var period = CareGuard.Text(input.Period, 2, 40, "Reporting period");
        var owner = CareGuard.Text(input.Owner, 2, 120, "Indicator owner");
        var indicator = new QualityIndicator(Guid.NewGuid(), code, name, definition, numerator, denominator, period, owner, "Active");
        await s.InsertIndicatorAsync(indicator, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "quality.indicator.defined", "QualityIndicator", indicator.Id, changes: new { code }), ct);
        return indicator;
    }, ct);

    public Task<IndicatorResult> RecordIndicatorResult(Scope scope, Guid id, QualityIndicatorResultInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.QualityIndicator); await CareGuard.Module(s, "quality", ct);
        var indicator = await s.FindIndicatorAsync(id, ct) ?? throw new DomainException("not_found", "Indicator not found.", 404);
        Guard(input.PeriodStart.Length == 7 && input.PeriodEnd.Length == 7, "quality.indicator", "Use yyyy-MM reporting periods.");
        Guard(string.CompareOrdinal(input.PeriodStart, input.PeriodEnd) <= 0, "quality.indicator", "Period start must not exceed period end.");
        Guard(input.Numerator >= 0 && input.Denominator > 0, "quality.indicator", "Numerator must be non-negative and denominator positive.");
        Guard(input.Numerator <= input.Denominator, "quality.indicator", "Numerator cannot exceed the denominator.");
        var value = Math.Round(input.Numerator / input.Denominator * 100, 2);
        var basis = CareGuard.Text(input.Basis, 2, 500, "Calculation basis");
        var note = CareGuard.Text(input.Note, 0, 1000, "Note");
        var result = new IndicatorResult(Guid.NewGuid(), id, input.PeriodStart, input.PeriodEnd,
            input.Numerator, input.Denominator, value, basis, note, scope.Actor.Name, clock.UtcNow);
        await s.InsertIndicatorResultAsync(result, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "quality.indicator.recorded", "IndicatorResult", result.Id, changes: new { input.PeriodStart, value }), ct);
        return result;
    }, ct);

    public Task<QualityDocument> CreateDocument(Scope scope, DocumentInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.QualityDocument); await CareGuard.Module(s, "quality", ct);
        Guard(DocCategories.Contains(input.Category), "quality.fields", "Choose a valid document category.");
        var title = CareGuard.Text(input.Title, 3, 300, "Document title");
        var owner = CareGuard.Text(input.Owner, 2, 120, "Document owner");
        var summary = CareGuard.Text(input.Summary, 5, 1000, "Version summary");
        var content = CareGuard.Text(input.Content, 20, 20000, "Document content");
        var number = await s.NextNumberAsync("DOC", $"{scope.BranchId}:{clock.UtcNow.Year}", $"DOC-{clock.UtcNow.Year}", ct);
        var document = new QualityDocument(Guid.NewGuid(), number, title, input.Category, "Draft", 1, owner, clock.UtcNow);
        await s.InsertQualityDocumentAsync(document, ct);
        var version = new DocumentVersion(Guid.NewGuid(), document.Id, 1, summary, content, scope.Actor.Name, clock.UtcNow, "", null);
        await s.InsertDocumentVersionAsync(version, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "quality.document.created", "QualityDocument", document.Id, title, new { input.Category }), ct);
        return document;
    }, ct);

    public Task<QualityDocument> NewDocumentVersion(Scope scope, Guid id, DocumentVersionInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.QualityDocument); await CareGuard.Module(s, "quality", ct);
        var document = await s.FindQualityDocumentAsync(id, ct) ?? throw new DomainException("not_found", "Document not found.", 404);
        Guard(document.Status is "Draft" or "InReview" or "Rejected", "quality.state", "Only draft documents accept new versions; published documents must be retired first.");
        var summary = CareGuard.Text(input.Summary, 5, 1000, "Version summary");
        var content = CareGuard.Text(input.Content, 20, 20000, "Document content");
        var next = document.CurrentVersion + 1;
        var version = new DocumentVersion(Guid.NewGuid(), id, next, summary, content, scope.Actor.Name, clock.UtcNow, "", null);
        await s.InsertDocumentVersionAsync(version, ct);
        var updated = document with { CurrentVersion = next, Status = "Draft", Version = document.Version + 1 };
        await s.UpdateQualityDocumentAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "quality.document.versioned", "QualityDocument", id, summary, new { Version = next }), ct);
        return updated;
    }, ct);

    public Task<QualityDocument> SubmitDocument(Scope scope, Guid id, DocumentTransitionInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.QualityDocument); await CareGuard.Module(s, "quality", ct);
        var document = await s.FindQualityDocumentAsync(id, ct) ?? throw new DomainException("not_found", "Document not found.", 404);
        CareGuard.Version(document.Version, input.Version);
        Guard(document.Status is "Draft" or "Rejected", "quality.state", "Only draft documents can be submitted for review.");
        _ = CareGuard.Text(input.Reason, 0, 1000, "Reason");
        var updated = document with { Status = "InReview", Version = document.Version + 1 };
        await s.UpdateQualityDocumentAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "quality.document.submitted", "QualityDocument", id, input.Reason), ct);
        return updated;
    }, ct);

    public Task<QualityDocument> ApproveDocument(Scope scope, Guid id, DocumentTransitionInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.QualityDocument); await CareGuard.Module(s, "quality", ct);
        var document = await s.FindQualityDocumentAsync(id, ct) ?? throw new DomainException("not_found", "Document not found.", 404);
        CareGuard.Version(document.Version, input.Version);
        Guard(document.Status == "InReview", "quality.state", "Only documents under review can be approved.");
        var versions = await s.ListDocumentVersionsAsync(id, ct);
        var current = versions.FirstOrDefault(v => v.Number == document.CurrentVersion) ?? throw new DomainException("not_found", "Document version not found.", 404);
        Guard(current.CreatedBy != scope.Actor.Name, "quality.separation", "The version author cannot approve their own document.", 403);
        _ = CareGuard.Text(input.Reason, 0, 1000, "Reason");
        await s.UpdateDocumentVersionAsync(current with { ApprovedBy = scope.Actor.Name, ApprovedAt = clock.UtcNow }, ct);
        var updated = document with { Status = "Approved", Version = document.Version + 1 };
        await s.UpdateQualityDocumentAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "quality.document.approved", "QualityDocument", id, input.Reason), ct);
        return updated;
    }, ct);

    public Task<QualityDocument> PublishDocument(Scope scope, Guid id, DocumentTransitionInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.QualityDocument); await CareGuard.Module(s, "quality", ct);
        var document = await s.FindQualityDocumentAsync(id, ct) ?? throw new DomainException("not_found", "Document not found.", 404);
        CareGuard.Version(document.Version, input.Version);
        Guard(document.Status == "Approved", "quality.state", "Only approved documents can be published.");
        _ = CareGuard.Text(input.Reason, 0, 1000, "Reason");
        var updated = document with { Status = "Published", Version = document.Version + 1 };
        await s.UpdateQualityDocumentAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "quality.document.published", "QualityDocument", id, input.Reason), ct);
        return updated;
    }, ct);

    public Task<QualityDocument> RetireDocument(Scope scope, Guid id, DocumentTransitionInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.QualityDocument); await CareGuard.Module(s, "quality", ct);
        var document = await s.FindQualityDocumentAsync(id, ct) ?? throw new DomainException("not_found", "Document not found.", 404);
        CareGuard.Version(document.Version, input.Version);
        Guard(document.Status == "Published", "quality.state", "Only published documents can be retired.");
        _ = CareGuard.Text(input.Reason, 5, 1000, "Retirement reason");
        var updated = document with { Status = "Retired", Version = document.Version + 1 };
        await s.UpdateQualityDocumentAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "quality.document.retired", "QualityDocument", id, input.Reason), ct);
        return updated;
    }, ct);

    public Task<SopAcknowledgment> AcknowledgeDocument(Scope scope, Guid id, DocumentAcknowledgeInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.QualityRead); await CareGuard.Module(s, "quality", ct);
        var document = await s.FindQualityDocumentAsync(id, ct) ?? throw new DomainException("not_found", "Document not found.", 404);
        Guard(document.Status == "Published", "quality.state", "Only published documents can be acknowledged.");
        var attestation = CareGuard.Text(input.Attestation, 3, 500, "Attestation");
        var existing = await s.ListSopAcknowledgmentsAsync(id, ct);
        Guard(existing.All(a => !(a.VersionNumber == document.CurrentVersion && a.UserName == scope.Actor.Name)),
            "quality.duplicate", "You have already acknowledged this version.");
        var ack = new SopAcknowledgment(Guid.NewGuid(), id, document.CurrentVersion, scope.Actor.Name, attestation, clock.UtcNow);
        await s.InsertSopAcknowledgmentAsync(ack, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "quality.document.acknowledged", "SopAcknowledgment", ack.Id, attestation, new { document.CurrentVersion }), ct);
        return ack;
    }, ct);

    public Task<IReadOnlyList<Infection>> ListInfectionsAsync(Scope scope, Guid? patientId, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.QualityRead); await CareGuard.Module(s, "quality", ct);
        return await s.ListInfectionsAsync(patientId, ct);
    }, ct);
    public Task<IReadOnlyList<Incident>> ListIncidentsAsync(Scope scope, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.QualityRead); await CareGuard.Module(s, "quality", ct);
        return await s.ListIncidentsAsync(ct);
    }, ct);
    public Task<IReadOnlyList<RiskAssessment>> ListRisksAsync(Scope scope, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.QualityRead); await CareGuard.Module(s, "quality", ct);
        return await s.ListRisksAsync(ct);
    }, ct);
    public Task<IReadOnlyList<Capa>> ListCapasAsync(Scope scope, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.QualityRead); await CareGuard.Module(s, "quality", ct);
        return await s.ListCapasAsync(ct);
    }, ct);
    public Task<IReadOnlyList<ClinicalAudit>> ListAuditsAsync(Scope scope, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.QualityRead); await CareGuard.Module(s, "quality", ct);
        return await s.ListAuditsAsync(ct);
    }, ct);
    public Task<IReadOnlyList<QualityIndicator>> ListIndicatorsAsync(Scope scope, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.QualityRead); await CareGuard.Module(s, "quality", ct);
        return await s.ListIndicatorsAsync(ct);
    }, ct);
    public Task<IReadOnlyList<IndicatorResult>> ListIndicatorResultsAsync(Scope scope, Guid? indicatorId, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.QualityRead); await CareGuard.Module(s, "quality", ct);
        return await s.ListIndicatorResultsAsync(indicatorId, ct);
    }, ct);
    public Task<IReadOnlyList<QualityDocument>> ListDocumentsAsync(Scope scope, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.QualityRead); await CareGuard.Module(s, "quality", ct);
        return await s.ListQualityDocumentsAsync(ct);
    }, ct);
    public Task<IReadOnlyList<DocumentVersion>> ListDocumentVersionsAsync(Scope scope, Guid id, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.QualityRead); await CareGuard.Module(s, "quality", ct);
        return await s.ListDocumentVersionsAsync(id, ct);
    }, ct);
    public Task<IReadOnlyList<SopAcknowledgment>> ListAcknowledgmentsAsync(Scope scope, Guid id, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.QualityRead); await CareGuard.Module(s, "quality", ct);
        return await s.ListSopAcknowledgmentsAsync(id, ct);
    }, ct);
}

public sealed record PortalProfile(PortalAccount Account, Patient Patient, PortalPreference? Preferences,
    IReadOnlyList<PatientLink> Links, IReadOnlyList<ProxyGrant> ProxyGrants);
public sealed record PortalDocument(string Kind, string Title, string ContentType, string Content, string Watermark);

public sealed class PortalService(IUnitOfWork unitOfWork, IClock clock)
{
    static void Guard(bool ok, string code, string message, int status = 422) { if (!ok) throw new DomainException(code, message, status); }
    static readonly string[] Relationships = ["Self", "Parent", "Spouse", "Guardian", "Proxy"];
    static readonly string[] GrantScopes = ["Appointments", "Records", "Billing"];
    static readonly string[] PaymentMethods = ["Cash", "Card", "UPI", "Bank transfer"];

    async Task<PortalAccount> Account(IHospitalStore s, Scope scope, CancellationToken ct) =>
        await s.FindPortalAccountByUserIdAsync(scope.Actor.Id, ct) ?? throw new DomainException("portal.identity", "No verified portal account is linked to this login.", 404);

    async Task RequireAccess(IHospitalStore s, PortalAccount account, Guid patientId, string need, CancellationToken ct)
    {
        if (account.PatientId == patientId) return;
        var link = await s.FindPatientLinkAsync(account.Id, patientId, ct);
        Guard(link is { Status: "Active" }, "portal.ownership", "This record does not belong to your portal account.", 403);
        var grant = await s.FindActiveProxyGrantAsync(patientId, account.Id, need, DateOnly.FromDateTime(clock.UtcNow.UtcDateTime), ct);
        Guard(grant is not null, "portal.ownership", $"Your proxy access does not include {need}.", 403);
    }

    public Task<PortalAccount> Register(Scope scope, PortalRegisterInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.PortalAdmin); await CareGuard.Module(s, "portal", ct);
        _ = await s.FindPatientAsync(input.PatientId, ct) ?? throw new DomainException("not_found", "Patient not found.", 404);
        Guard((await s.FindPortalAccountByPatientAsync(input.PatientId, ct)) is null, "portal.duplicate", "A portal account already exists for this patient.");
        var userName = CareGuard.Text(input.UserName, 3, 60, "Portal user name");
        var evidence = CareGuard.Text(input.ProofingEvidence, 5, 500, "Identity proofing evidence");
        _ = await s.FindUserAsync(input.UserId, scope.HospitalId, scope.BranchId, ct) ?? throw new DomainException("not_found", "Portal login user not found.", 404);
        var account = new PortalAccount(Guid.NewGuid(), input.UserId, input.PatientId, userName, "Pending", evidence, "", null, clock.UtcNow);
        await s.InsertPortalAccountAsync(account, ct);
        await s.InsertPatientLinkAsync(new PatientLink(Guid.NewGuid(), account.Id, input.PatientId, "Self", "Active", evidence, clock.UtcNow), ct);
        await s.InsertPortalPreferenceAsync(new PortalPreference(Guid.NewGuid(), account.Id, "SMS", "English", true), ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "portal.account.registered", "PortalAccount", account.Id, changes: new { input.PatientId }), ct);
        return account;
    }, ct);

    public Task<PortalAccount> VerifyAccount(Scope scope, Guid id, PortalVerifyInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.PortalAdmin); await CareGuard.Module(s, "portal", ct);
        var account = await s.FindPortalAccountAsync(id, ct) ?? throw new DomainException("not_found", "Portal account not found.", 404);
        CareGuard.Version(account.Version, input.Version);
        Guard(account.Status == "Pending", "portal.state", "Only pending accounts can be verified.");
        var by = CareGuard.Text(input.VerifiedBy, 2, 120, "Verifier");
        var updated = account with { Status = "Verified", VerifiedBy = by, VerifiedAt = clock.UtcNow, Version = account.Version + 1 };
        await s.UpdatePortalAccountAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "portal.account.verified", "PortalAccount", id, by), ct);
        return updated;
    }, ct);

    public Task<PatientLink> LinkPatient(Scope scope, PortalLinkInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.PortalRead); await CareGuard.Module(s, "portal", ct);
        var account = await Account(s, scope, ct);
        Guard(account.Status == "Verified", "portal.state", "Verify the portal account before linking patients.");
        _ = await s.FindPatientAsync(input.PatientId, ct) ?? throw new DomainException("not_found", "Patient not found.", 404);
        Guard(Relationships.Contains(input.Relationship) && input.Relationship != "Self", "portal.fields", "Choose a valid relationship for the link.");
        var evidence = CareGuard.Text(input.ProofingEvidence, 5, 500, "Link proofing evidence");
        Guard(await s.FindPatientLinkAsync(account.Id, input.PatientId, ct) is null, "portal.duplicate", "This patient is already linked to the account.");
        var link = new PatientLink(Guid.NewGuid(), account.Id, input.PatientId, input.Relationship, "Active", evidence, clock.UtcNow);
        await s.InsertPatientLinkAsync(link, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "portal.link.created", "PatientLink", link.Id, changes: new { input.PatientId, input.Relationship }), ct);
        return link;
    }, ct);

    public Task<ProxyGrant> GrantProxy(Scope scope, Guid patientId, ProxyGrantInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.PortalConsent); await CareGuard.Module(s, "portal", ct);
        var account = await Account(s, scope, ct);
        Guard(account.PatientId == patientId, "portal.ownership", "Only the record owner can grant proxy access.", 403);
        var target = await s.FindPortalAccountAsync(input.AccountId, ct) ?? throw new DomainException("not_found", "Proxy portal account not found.", 404);
        Guard(target.Id != account.Id, "portal.fields", "Proxy access is granted to a different portal account.");
        Guard(GrantScopes.Contains(input.Scope), "portal.fields", "Scope must be Appointments, Records or Billing.");
        Guard(input.ExpiresOn > DateOnly.FromDateTime(clock.UtcNow.UtcDateTime), "portal.fields", "Proxy expiry must be in the future.");
        var existing = await s.FindPatientLinkAsync(target.Id, patientId, ct);
        if (existing is null)
            await s.InsertPatientLinkAsync(new PatientLink(Guid.NewGuid(), target.Id, patientId, "Proxy", "Active", "Granted by record owner", clock.UtcNow), ct);
        var grant = new ProxyGrant(Guid.NewGuid(), patientId, target.Id, input.Scope, input.ExpiresOn, "Active",
            scope.Actor.Name, clock.UtcNow, "", null);
        await s.InsertProxyGrantAsync(grant, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "portal.proxy.granted", "ProxyGrant", grant.Id, changes: new { input.Scope, input.ExpiresOn }), ct);
        return grant;
    }, ct);

    public Task<ProxyGrant> RevokeProxy(Scope scope, Guid id, ProxyRevokeInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.PortalConsent); await CareGuard.Module(s, "portal", ct);
        var grant = await s.FindProxyGrantAsync(id, ct) ?? throw new DomainException("not_found", "Proxy grant not found.", 404);
        CareGuard.Version(grant.Version, input.Version);
        Guard(grant.Status == "Active", "portal.state", "Only active proxy grants can be revoked.");
        var account = await Account(s, scope, ct);
        Guard(account.PatientId == grant.PatientId, "portal.ownership", "Only the record owner can revoke proxy access.", 403);
        _ = CareGuard.Text(input.Reason, 5, 500, "Revocation reason");
        var updated = grant with { Status = "Revoked", RevokedBy = scope.Actor.Name, RevokedAt = clock.UtcNow, Version = grant.Version + 1 };
        await s.UpdateProxyGrantAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "portal.proxy.revoked", "ProxyGrant", id, input.Reason), ct);
        return updated;
    }, ct);

    public Task<DeviceSession> Login(Scope scope, PortalLoginInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.PortalRead); await CareGuard.Module(s, "portal", ct);
        var account = await Account(s, scope, ct);
        Guard(account.Status == "Verified", "portal.state", "Only verified portal accounts can sign in.");
        var device = CareGuard.Text(input.Device, 2, 120, "Device");
        var token = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
        var session = new DeviceSession(Guid.NewGuid(), account.Id, device, hash, clock.UtcNow, clock.UtcNow.AddDays(30), "Active");
        await s.InsertDeviceSessionAsync(session, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "portal.login", "DeviceSession", session.Id, device), ct);
        return session with { TokenHash = token };
    }, ct);

    public Task<PortalPreference> UpdatePreferences(Scope scope, PortalPreferenceInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.PortalRead); await CareGuard.Module(s, "portal", ct);
        var account = await Account(s, scope, ct);
        Guard(input.ReminderChannel is "SMS" or "Email" or "None", "portal.fields", "Reminder channel must be SMS, Email or None.");
        var language = CareGuard.Text(input.Language, 2, 40, "Language");
        var existing = await s.FindPortalPreferenceAsync(account.Id, ct) ?? throw new DomainException("not_found", "Portal preferences not found.", 404);
        var updated = existing with { ReminderChannel = input.ReminderChannel, Language = language, PaperlessBilling = input.PaperlessBilling, Version = existing.Version + 1 };
        await s.UpdatePortalPreferenceAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "portal.preferences.updated", "PortalPreference", updated.Id, changes: new { input.ReminderChannel, input.PaperlessBilling }), ct);
        return updated;
    }, ct);

    public Task<PortalProfile> GetProfile(Scope scope, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.PortalRead); await CareGuard.Module(s, "portal", ct);
        var account = await Account(s, scope, ct);
        var patient = await s.FindPatientAsync(account.PatientId, ct) ?? throw new DomainException("not_found", "Patient not found.", 404);
        var links = await s.ListPatientLinksAsync(account.Id, ct);
        var grants = (await s.ListProxyGrantsAsync(account.PatientId, ct)).Where(g => g.AccountId == account.Id || g.PatientId == account.PatientId).ToList();
        return new PortalProfile(account, patient, await s.FindPortalPreferenceAsync(account.Id, ct), links, grants);
    }, ct);

    async Task<(PortalAccount Account, Guid PatientId)> Target(IHospitalStore s, Scope scope, Guid? patientId, string need, CancellationToken ct)
    {
        var account = await Account(s, scope, ct);
        var target = patientId ?? account.PatientId;
        if (target != account.PatientId) await RequireAccess(s, account, target, need, ct);
        return (account, target);
    }

    public Task<Chart> GetAppointments(Scope scope, Guid? patientId, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.PortalRead); await CareGuard.Module(s, "portal", ct);
        var (_, target) = await Target(s, scope, patientId, "Appointments", ct);
        return await s.LoadPortalChartAsync(target, 100, ct) ?? throw new DomainException("not_found", "Portal record not found.", 404);
    }, ct);

    public Task<IReadOnlyList<Encounter>> GetPrescriptions(Scope scope, Guid? patientId, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.PortalRead); await CareGuard.Module(s, "portal", ct);
        var (_, target) = await Target(s, scope, patientId, "Records", ct);
        var chart = await s.LoadPortalChartAsync(target, 100, ct) ?? throw new DomainException("not_found", "Portal record not found.", 404);
        return (IReadOnlyList<Encounter>)chart.Encounters.Where(e => e.SignedAt is not null && e.Prescription.Count > 0).ToList();
    }, ct);

    public Task<IReadOnlyList<Encounter>> GetSummaries(Scope scope, Guid? patientId, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.PortalRead); await CareGuard.Module(s, "portal", ct);
        var (_, target) = await Target(s, scope, patientId, "Records", ct);
        var chart = await s.LoadPortalChartAsync(target, 100, ct) ?? throw new DomainException("not_found", "Portal record not found.", 404);
        return (IReadOnlyList<Encounter>)chart.Encounters.Where(e => e.SignedAt is not null).ToList();
    }, ct);

    public Task<IReadOnlyList<Invoice>> GetBills(Scope scope, Guid? patientId, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.PortalRead); await CareGuard.Module(s, "portal", ct);
        var (_, target) = await Target(s, scope, patientId, "Billing", ct);
        var chart = await s.LoadPortalChartAsync(target, 100, ct) ?? throw new DomainException("not_found", "Portal record not found.", 404);
        return chart.Invoices;
    }, ct);

    public Task<object> GetReports(Scope scope, Guid? patientId, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.PortalRead); await CareGuard.Module(s, "portal", ct);
        var (_, target) = await Target(s, scope, patientId, "Records", ct);
        var orders = await s.ListPortalLabOrdersAsync(target, ct);
        var tests = await s.ListLabTestsAsync(ct);
        var labs = new List<object>();
        foreach (var order in orders.Where(o => o.Status == "Authorized"))
        {
            var results = await s.ListPortalLabResultsAsync(order.Id, ct);
            var test = tests.FirstOrDefault(t => t.Id == order.TestId);
            labs.Add(new { order.Id, order.Number, TestName = test?.Name ?? "Investigation", order.Status, order.AuthorizedAt, results });
        }
        var reports = (await s.ListPortalRadiologyReportsAsync(target, ct)).Where(r => r.Status == "Authorized").ToList();
        return (object)new { labs, radiology = reports };
    }, ct);

    public Task<Appointment> BookAppointment(Scope scope, PortalAppointmentInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.PortalBook); await CareGuard.Module(s, "portal", ct);
        var account = await Account(s, scope, ct);
        Guard(account.Status == "Verified", "portal.state", "Only verified portal accounts can book appointments.");
        Guard(input.StartsAt > clock.UtcNow, "care.fields", "Appointments must be booked for a future time.");
        Guard(input.DurationMinutes is >= 10 and <= 60, "care.fields", "Duration must be between 10 and 60 minutes.");
        Guard(await s.HasActivePractitionerAsync(input.DoctorId, ct), "care.reference", "The selected practitioner is not active.");
        var reason = CareGuard.Text(input.Reason, 2, 300, "Visit reason");
        Guard(!await s.HasAppointmentConflictAsync(input.DoctorId, input.StartsAt, input.StartsAt.AddMinutes(input.DurationMinutes), null, ct),
            "care.conflict", "The practitioner is not available at this time.");
        var appointment = new Appointment(Guid.NewGuid(), account.PatientId, input.DoctorId, input.StartsAt,
            input.DurationMinutes, "In person", reason, "Scheduled", null, clock.UtcNow);
        await s.InsertAppointmentAsync(appointment, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "portal.appointment.booked", "Appointment", appointment.Id, changes: new { input.StartsAt }), ct);
        return appointment;
    }, ct);

    public Task<Invoice> Pay(Scope scope, PortalPaymentInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.PortalRead); await CareGuard.Module(s, "portal", ct);
        var account = await Account(s, scope, ct);
        var invoice = await s.FindInvoiceAsync(input.InvoiceId, ct) ?? throw new DomainException("not_found", "Invoice not found.", 404);
        if (invoice.PatientId != account.PatientId) await RequireAccess(s, account, invoice.PatientId, "Billing", ct);
        Guard(invoice.Status == "Issued", "billing.state", "Only issued invoices accept payments.");
        Guard(PaymentMethods.Contains(input.Method), "billing.payment", "Choose a supported payment method.");
        var reference = CareGuard.Text(input.Reference, 2, 100, "Payment reference");
        Guard(input.Amount > 0, "billing.payment", "Payment amount must be positive.");
        Guard(invoice.Payments.Sum(p => p.Amount) + input.Amount <= invoice.Total, "billing.payment", "Payments cannot exceed the invoice total.");
        var payment = new Payment(Guid.NewGuid(), input.Amount, input.Method, reference, clock.UtcNow, scope.Actor.Id);
        var updated = invoice with
        {
            Payments = [.. invoice.Payments, payment],
            Status = invoice.Payments.Sum(p => p.Amount) + input.Amount == invoice.Total ? "Paid" : invoice.Status,
            Version = invoice.Version + 1
        };
        await s.UpdateInvoiceAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "portal.payment.recorded", "Invoice", invoice.Id, changes: new { input.Amount, input.Method }), ct);
        return updated;
    }, ct);

    public Task<object> RecordConsent(Scope scope, PortalConsentInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.PortalConsent); await CareGuard.Module(s, "portal", ct);
        var account = await Account(s, scope, ct);
        var kind = CareGuard.Text(input.Kind, 2, 40, "Consent kind");
        Guard(kind is "TreatmentConsent" or "DataSharing" or "Telemedicine" or "ProxyAccess", "portal.fields", "Unknown consent kind.");
        var note = CareGuard.Text(input.Note, 0, 500, "Note");
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "portal.consent.recorded", "PortalAccount", account.Id, note, new { kind, input.Given }), ct);
        return (object)new { kind, input.Given, note, recordedAt = clock.UtcNow };
    }, ct);

    public Task<PortalDocument> Download(Scope scope, Guid id, string kind, Guid? patientId, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.PortalDownload); await CareGuard.Module(s, "portal", ct);
        var (account, target) = await Target(s, scope, patientId, "Records", ct);
        var watermark = $"Careflow portal · {account.UserName} · {clock.UtcNow:O}";
        if (kind == "lab")
        {
            var order = (await s.ListPortalLabOrdersAsync(target, ct)).FirstOrDefault(o => o.Id == id)
                ?? throw new DomainException("not_found", "Lab report not found.", 404);
            Guard(order.Status == "Authorized", "portal.state", "Only authorized reports are available for download.");
            var results = await s.ListPortalLabResultsAsync(order.Id, ct);
            var test = (await s.ListLabTestsAsync(ct)).FirstOrDefault(t => t.Id == order.TestId);
            var content = string.Join("\n", results.Select(r => $"{test?.Name ?? "Investigation"}: {r.Value} {r.Unit} (ref {r.ReferenceLow}-{r.ReferenceHigh})"));
            await s.AddAuditAsync(CareGuard.Event(scope, clock, "portal.document.downloaded", "LabOrder", id, kind), ct);
            return new PortalDocument("lab", test?.Name ?? order.Number, "text/plain", content, watermark);
        }
        if (kind == "radiology")
        {
            var report = (await s.ListPortalRadiologyReportsAsync(target, ct)).FirstOrDefault(r => r.Id == id)
                ?? throw new DomainException("not_found", "Radiology report not found.", 404);
            Guard(report.Status == "Authorized", "portal.state", "Only authorized reports are available for download.");
            await s.AddAuditAsync(CareGuard.Event(scope, clock, "portal.document.downloaded", "RadiologyReport", id, kind), ct);
            return new PortalDocument("radiology", report.Impression, "text/plain", $"Findings: {report.Findings}\nImpression: {report.Impression}", watermark);
        }
        if (kind == "invoice")
        {
            var chart = await s.LoadPortalChartAsync(target, 100, ct) ?? throw new DomainException("not_found", "Portal record not found.", 404);
            var invoice = chart.Invoices.FirstOrDefault(i => i.Id == id) ?? throw new DomainException("not_found", "Invoice not found.", 404);
            var content = string.Join("\n", invoice.Lines.Select(l => $"{l.Name} × {l.Quantity} @ {l.UnitPrice:C}")) + $"\nTotal: {invoice.Total:C}";
            await s.AddAuditAsync(CareGuard.Event(scope, clock, "portal.document.downloaded", "Invoice", id, kind), ct);
            return new PortalDocument("invoice", invoice.Number, "text/plain", content, watermark);
        }
        throw new DomainException("not_found", "Unknown document kind.", 404);
    }, ct);
}

public sealed record DashboardView(string Name, DateTimeOffset GeneratedAt, bool Provisional, IReadOnlyList<DashboardCard> Cards);

public sealed class ReportingService(IUnitOfWork unitOfWork, IClock clock)
{
    static void Guard(bool ok, string code, string message, int status = 422) { if (!ok) throw new DomainException(code, message, status); }
    static readonly string[] Categories = ["Clinical", "Financial", "Management", "Quality"];
    static readonly string[] Cadences = ["Daily", "Weekly", "Monthly"];
    static readonly string[] Formats = ["PDF", "Excel"];
    // Server-side registry: report definitions select a governed query key — users never supply SQL.
    static readonly string[] QueryKeys = ["census_summary", "revenue_by_day", "opd_volume_by_doctor", "claims_approval_rate", "quality_incident_trend", "capa_status"];

    static DateOnly? ParseDate(string? value) => DateOnly.TryParse(value, out var d) ? d : null;

    async Task<IReadOnlyList<Dictionary<string, object?>>> RunQuery(IHospitalStore s, string key, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var rows = new List<Dictionary<string, object?>>();
        var patients = await s.ListPatientsBriefAsync(ct);
        var appointments = await s.ListAppointmentsBriefAsync(ct);
        var encounters = await s.ListEncountersBriefAsync(ct);
        var invoices = await s.ListInvoicesBriefAsync(ct);
        switch (key)
        {
            case "census_summary":
                foreach (var p in patients.Take(500))
                {
                    var enc = encounters.Count(e => e.PatientId == p.Id);
                    var inv = invoices.Where(i => i.PatientId == p.Id).ToList();
                    rows.Add(new() { ["uhid"] = p.Uhid, ["patient"] = p.FullName, ["encounters"] = enc, ["invoices"] = inv.Count, ["outstanding"] = inv.Sum(i => i.Total - i.Payments.Sum(x => x.Amount)) });
                }
                break;
            case "revenue_by_day":
                var byDay = invoices.SelectMany(i => i.Payments)
                    .Where(p => (from is null || DateOnly.FromDateTime(p.ReceivedAt.UtcDateTime) >= from) && (to is null || DateOnly.FromDateTime(p.ReceivedAt.UtcDateTime) <= to))
                    .GroupBy(p => DateOnly.FromDateTime(p.ReceivedAt.UtcDateTime)).OrderBy(g => g.Key);
                foreach (var g in byDay.Take(500))
                    rows.Add(new() { ["date"] = g.Key.ToString("yyyy-MM-dd"), ["collected"] = g.Sum(x => x.Amount), ["payments"] = g.Count() });
                break;
            case "opd_volume_by_doctor":
                var byDoctor = appointments
                    .Where(a => (from is null || DateOnly.FromDateTime(a.StartsAt.UtcDateTime) >= from) && (to is null || DateOnly.FromDateTime(a.StartsAt.UtcDateTime) <= to))
                    .GroupBy(a => a.DoctorId);
                foreach (var g in byDoctor.Take(500))
                    rows.Add(new() { ["doctorId"] = g.Key, ["visits"] = g.Count(), ["cancelled"] = g.Count(a => a.Status == "Cancelled") });
                break;
            case "claims_approval_rate":
                foreach (var g in (await s.ListClaimsAsync(null, ct)).GroupBy(c => c.Status).Take(500))
                    rows.Add(new() { ["status"] = g.Key, ["claims"] = g.Count(), ["amount"] = g.Sum(c => c.TotalAmount), ["approved"] = g.Sum(c => c.ApprovedAmount) });
                break;
            case "quality_incident_trend":
                foreach (var g in (await s.ListIncidentsAsync(ct))
                    .Where(i => (from is null || DateOnly.FromDateTime(i.ReportedAt.UtcDateTime) >= from) && (to is null || DateOnly.FromDateTime(i.ReportedAt.UtcDateTime) <= to))
                    .GroupBy(i => i.Category).Take(500))
                    rows.Add(new() { ["category"] = g.Key, ["incidents"] = g.Count(), ["open"] = g.Count(i => i.Status != "Closed") });
                break;
            case "capa_status":
                foreach (var g in (await s.ListCapasAsync(ct)).GroupBy(c => c.Status).Take(500))
                    rows.Add(new() { ["status"] = g.Key, ["capas"] = g.Count(), ["overdue"] = g.Count(c => c.Status != "Closed" && c.DueDate < clock.UtcNow) });
                break;
        }
        return rows;
    }

    public Task<ReportDefinition> Define(Scope scope, ReportDefinitionInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.ReportDefine); await CareGuard.Module(s, "reports", ct);
        var code = CareGuard.Text(input.Code, 2, 20, "Definition code").ToUpperInvariant();
        Guard((await s.FindReportDefinitionByCodeAsync(code, ct)) is null, "report.duplicate", "A report definition with this code already exists.");
        var name = CareGuard.Text(input.Name, 3, 200, "Definition name");
        Guard(Categories.Contains(input.Category), "report.fields", "Category must be Clinical, Financial, Management or Quality.");
        Guard(QueryKeys.Contains(input.QueryKey), "report.query", "Choose a governed query; arbitrary queries are not accepted.");
        var owner = CareGuard.Text(input.Owner, 2, 120, "Definition owner");
        var columns = CareGuard.Text(input.Columns, 2, 500, "Column list");
        var filters = CareGuard.Text(input.Filters, 0, 500, "Filter metadata");
        var definition = new ReportDefinition(Guid.NewGuid(), code, name, input.Category, "Draft", 1, owner, clock.UtcNow);
        await s.InsertReportDefinitionAsync(definition, ct);
        await s.InsertReportVersionAsync(new ReportVersion(Guid.NewGuid(), definition.Id, 1, input.QueryKey, columns, filters, scope.Actor.Name, clock.UtcNow, "", null), ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "report.definition.created", "ReportDefinition", definition.Id, changes: new { code, input.QueryKey }), ct);
        return definition;
    }, ct);

    public Task<ReportDefinition> ApproveDefinition(Scope scope, Guid id, VersionInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.ReportDefine); await CareGuard.Module(s, "reports", ct);
        var definition = await s.FindReportDefinitionAsync(id, ct) ?? throw new DomainException("not_found", "Report definition not found.", 404);
        CareGuard.Version(definition.Version, input.Version);
        Guard(definition.Status == "Draft", "report.state", "Only draft definitions can be approved.");
        Guard(definition.Owner != scope.Actor.Name, "report.separation", "The definition owner cannot approve their own definition.", 403);
        var updated = definition with { Status = "Active", Version = definition.Version + 1 };
        await s.UpdateReportDefinitionAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "report.definition.approved", "ReportDefinition", id, input.Reason), ct);
        return updated;
    }, ct);

    public Task<ReportExecution> Execute(Scope scope, ReportExecuteInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.ReportExecute); await CareGuard.Module(s, "reports", ct);
        var definition = await s.FindReportDefinitionAsync(input.DefinitionId, ct) ?? throw new DomainException("not_found", "Report definition not found.", 404);
        Guard(definition.Status == "Active", "report.state", "Only approved definitions can be executed.");
        DateOnly? from = null, to = null;
        if (!string.IsNullOrWhiteSpace(input.Parameters))
        {
            System.Text.Json.JsonDocument doc;
            try { doc = System.Text.Json.JsonDocument.Parse(input.Parameters); }
            catch (System.Text.Json.JsonException) { throw new DomainException("report.parameters", "Parameters must be valid JSON."); }
            Guard(doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object, "report.parameters", "Parameters must be a JSON object.");
            foreach (var property in doc.RootElement.EnumerateObject())
                Guard(property.Name is "from" or "to", "report.parameters", $"Unknown parameter '{property.Name}'.");
            from = doc.RootElement.TryGetProperty("from", out var f) ? ParseDate(f.GetString()) : null;
            to = doc.RootElement.TryGetProperty("to", out var t) ? ParseDate(t.GetString()) : null;
            Guard(from is null || to is null || from <= to, "report.parameters", "The from date must not exceed the to date.");
            Guard(from is null || to is null || to.Value.DayNumber - from.Value.DayNumber <= 366, "report.parameters", "Date range cannot exceed one year.");
        }
        var versions = await s.ListReportVersionsAsync(definition.Id, ct);
        var version = versions.OrderBy(v => v.Number).Last();
        var rows = await RunQuery(s, version.QueryKey, from, to, ct);
        var execution = new ReportExecution(Guid.NewGuid(),
            await s.NextNumberAsync("RPT", $"{scope.BranchId}:{clock.UtcNow.Year}", $"RPT-{clock.UtcNow.Year}", ct),
            definition.Id, definition.CurrentVersion,
            string.IsNullOrWhiteSpace(input.Parameters) ? "{}" : input.Parameters,
            System.Text.Json.JsonSerializer.Serialize(rows), rows.Count, "Completed", scope.Actor.Name, clock.UtcNow, "");
        await s.InsertReportExecutionAsync(execution, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "report.executed", "ReportExecution", execution.Id, changes: new { rows.Count, version.QueryKey }), ct);
        return execution;
    }, ct);

    public Task<ReportExport> Export(Scope scope, ReportExportInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.ReportExport); await CareGuard.Module(s, "reports", ct);
        var execution = await s.FindReportExecutionAsync(input.ExecutionId, ct) ?? throw new DomainException("not_found", "Report execution not found.", 404);
        Guard(execution.Status == "Completed", "report.state", "Only completed executions can be exported.");
        Guard(Formats.Contains(input.Format), "report.fields", "Export format must be PDF or Excel.");
        var definition = await s.FindReportDefinitionAsync(execution.DefinitionId, ct) ?? throw new DomainException("not_found", "Report definition not found.", 404);
        var watermark = $"Careflow HIS · {definition.Code} · {scope.Actor.Name} · {clock.UtcNow:O} · {scope.BranchId}";
        var extension = input.Format == "PDF" ? "pdf" : "xlsx";
        var fileRef = $"{definition.Code}-{execution.Number}.{extension}";
        var export = new ReportExport(Guid.NewGuid(), execution.Id, input.Format, "Ready", watermark, fileRef, scope.Actor.Name, clock.UtcNow);
        await s.InsertReportExportAsync(export, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "report.exported", "ReportExport", export.Id, changes: new { input.Format, execution.RowCount }), ct);
        return export;
    }, ct);

    public Task<ReportSubscription> Subscribe(Scope scope, SubscriptionInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.ReportDefine); await CareGuard.Module(s, "reports", ct);
        _ = await s.FindReportDefinitionAsync(input.DefinitionId, ct) ?? throw new DomainException("not_found", "Report definition not found.", 404);
        Guard(Cadences.Contains(input.Cadence), "report.fields", "Cadence must be Daily, Weekly or Monthly.");
        var recipients = CareGuard.Text(input.Recipients, 3, 500, "Recipients");
        var subscription = new ReportSubscription(Guid.NewGuid(), input.DefinitionId, input.Cadence, recipients, true, scope.Actor.Name, clock.UtcNow);
        await s.InsertReportSubscriptionAsync(subscription, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "report.subscribed", "ReportSubscription", subscription.Id, changes: new { input.Cadence }), ct);
        return subscription;
    }, ct);

    public Task<ReportExecution> GetExecution(Scope scope, Guid id, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.ReportRead); await CareGuard.Module(s, "reports", ct);
        return await s.FindReportExecutionAsync(id, ct) ?? throw new DomainException("not_found", "Report execution not found.", 404);
    }, ct);

    public Task<IReadOnlyList<ReportDefinition>> ListDefinitionsAsync(Scope scope, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.ReportRead); await CareGuard.Module(s, "reports", ct);
        return await s.ListReportDefinitionsAsync(ct);
    }, ct);
    public Task<IReadOnlyList<ReportExecution>> ListExecutionsAsync(Scope scope, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.ReportRead); await CareGuard.Module(s, "reports", ct);
        return await s.ListReportExecutionsAsync(ct);
    }, ct);
    public Task<IReadOnlyList<ReportExport>> ListExportsAsync(Scope scope, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.ReportRead); await CareGuard.Module(s, "reports", ct);
        return await s.ListReportExportsAsync(null, ct);
    }, ct);

    public Task<DashboardView> Dashboard(Scope scope, string view, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.ReportRead); await CareGuard.Module(s, "reports", ct);
        if (view is not ("clinical" or "financial" or "management")) throw new DomainException("not_found", "Unknown dashboard view.", 404);
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var patients = await s.ListPatientsBriefAsync(ct);
        var appointments = await s.ListAppointmentsBriefAsync(ct);
        var encounters = await s.ListEncountersBriefAsync(ct);
        var invoices = await s.ListInvoicesBriefAsync(ct);
        var cards = new List<DashboardCard>();
        string Fresh() => clock.UtcNow.ToString("O");
        if (view == "clinical")
        {
            cards.Add(new("Signed encounters", encounters.Count(e => e.SignedAt is not null).ToString(), "All time", Fresh(), true));
            cards.Add(new("Encounters (7 days)", encounters.Count(e => e.SignedAt >= clock.UtcNow.AddDays(-7)).ToString(), "Signed records", Fresh(), true));
            cards.Add(new("OPD visits today", appointments.Count(a => DateOnly.FromDateTime(a.StartsAt.UtcDateTime) == today).ToString(), "Scheduled appointments", Fresh(), true));
            cards.Add(new("Infections this month", (await s.ListInfectionsAsync(null, ct)).Count(i => i.DetectedOn.Month == today.Month && i.DetectedOn.Year == today.Year).ToString(), "Surveillance reports", Fresh(), true));
            cards.Add(new("Open incidents", (await s.ListIncidentsAsync(ct)).Count(i => i.Status != "Closed").ToString(), "Under review", Fresh(), true));
            cards.Add(new("Overdue CAPA", (await s.ListCapasAsync(ct)).Count(c => c.Status is not ("Closed" or "Cancelled") && c.DueDate < clock.UtcNow).ToString(), "Past due date", Fresh(), true));
        }
        else if (view == "financial")
        {
            var total = invoices.Sum(i => i.Total);
            var collected = invoices.Sum(i => i.Payments.Sum(p => p.Amount));
            cards.Add(new("Billed", total.ToString("C"), $"{invoices.Count} invoices", Fresh(), true));
            cards.Add(new("Collected", collected.ToString("C"), "All payment records", Fresh(), true));
            cards.Add(new("Outstanding", (total - collected).ToString("C"), "Issued and draft invoices", Fresh(), true));
            cards.Add(new("Collected today", invoices.Sum(i => i.Payments.Where(p => DateOnly.FromDateTime(p.ReceivedAt.UtcDateTime) == today).Sum(p => p.Amount)).ToString("C"), "Receipts today", Fresh(), true));
            cards.Add(new("Claims approved", (await s.ListClaimsAsync(null, ct)).Sum(c => c.ApprovedAmount).ToString("C"), "Adjudicated amounts", Fresh(), true));
            decimal payouts = 0;
            foreach (var batch in await s.ListIncentiveBatchesAsync(ct)) payouts += (await s.ListIncentivePaymentsAsync(batch.Id, ct)).Sum(p => p.Amount);
            cards.Add(new("Incentive payouts", payouts.ToString("C"), "Paid batches", Fresh(), true));
        }
        else
        {
            cards.Add(new("Registered patients", patients.Count(p => p.Active).ToString(), $"{patients.Count} total", Fresh(), true));
            cards.Add(new("Appointments today", appointments.Count(a => DateOnly.FromDateTime(a.StartsAt.UtcDateTime) == today).ToString(), "All practitioners", Fresh(), true));
            cards.Add(new("Portal accounts verified", (await s.ListPortalAccountsAsync(ct)).Count(a => a.Status == "Verified").ToString(), "Patient portal", Fresh(), true));
            cards.Add(new("Published SOPs", (await s.ListQualityDocumentsAsync(ct)).Count(d => d.Status == "Published").ToString(), "Controlled documents", Fresh(), true));
            cards.Add(new("Report executions", (await s.ListReportExecutionsAsync(ct)).Count.ToString(), "Governed reporting", Fresh(), true));
            cards.Add(new("Quality indicators", (await s.ListIndicatorsAsync(ct)).Count(i => i.Status == "Active").ToString(), "Active definitions", Fresh(), true));
        }
        return new DashboardView(view, clock.UtcNow, true, cards);
    }, ct);
}
