namespace Careflow.Domain;

// ── Quality, infection control and accreditation (§24) ───────────────────────────────
public sealed record Infection(Guid Id, string Number, Guid PatientId, Guid? EncounterId, string Category,
    string Site, string DetectedMethod, DateOnly DetectedOn, string Severity, string Status,
    string ReportedBy, DateTimeOffset ReportedAt, string Investigation, string Outcome, long Version = 1);

public sealed record Incident(Guid Id, string Number, string Category, string Severity, string Description,
    string Location, Guid? PatientId, string ReportedBy, DateTimeOffset ReportedAt, string Status,
    string TriageNote, string AssignedTo, DateTimeOffset? TriagedAt, string Investigation,
    string RootCause, long Version = 1);

public sealed record RiskAssessment(Guid Id, string Number, string Domain, string Description,
    int Probability, int Impact, string Mitigation, string Owner, string Status,
    DateTimeOffset AssessedAt, long Version = 1)
{
    public int Score => Probability * Impact;
}

public sealed record Capa(Guid Id, string Number, Guid? IncidentId, Guid? InfectionId, Guid? AuditId,
    Guid? RiskId, string Correction, string CorrectiveAction, string PreventiveAction,
    string Owner, DateTimeOffset DueDate, string Status, string EffectivenessEvidence,
    bool EffectivenessVerified, string VerifiedBy, DateTimeOffset? VerifiedAt,
    DateTimeOffset CreatedAt, string CreatedBy, long Version = 1);

public sealed record ClinicalAudit(Guid Id, string Number, string Domain, string Scope, DateOnly PlannedFor,
    string Lead, string Status, string Findings, int ScorePercent, string Report,
    DateTimeOffset CreatedAt, string CreatedBy, long Version = 1);

public sealed record QualityIndicator(Guid Id, string Code, string Name, string Definition,
    string Numerator, string Denominator, string Period, string Owner, string Status, long Version = 1);

public sealed record IndicatorResult(Guid Id, Guid IndicatorId, string PeriodStart, string PeriodEnd,
    decimal Numerator, decimal Denominator, decimal Value, string Basis, string Note,
    string RecordedBy, DateTimeOffset RecordedAt);

public sealed record QualityDocument(Guid Id, string Number, string Title, string Category, string Status,
    int CurrentVersion, string Owner, DateTimeOffset CreatedAt, long Version = 1);

public sealed record DocumentVersion(Guid Id, Guid DocumentId, int Number, string Summary, string Content,
    string CreatedBy, DateTimeOffset CreatedAt, string ApprovedBy, DateTimeOffset? ApprovedAt);

public sealed record SopAcknowledgment(Guid Id, Guid DocumentId, int VersionNumber, string UserName,
    string Attestation, DateTimeOffset AcknowledgedAt);

// ── Patient portal (§25) ─────────────────────────────────────────────────────────────
public sealed record PortalAccount(Guid Id, Guid UserId, Guid PatientId, string UserName, string Status,
    string ProofingEvidence, string VerifiedBy, DateTimeOffset? VerifiedAt,
    DateTimeOffset CreatedAt, long Version = 1);

public sealed record PatientLink(Guid Id, Guid AccountId, Guid PatientId, string Relationship,
    string Status, string ProofingEvidence, DateTimeOffset LinkedAt, long Version = 1);

public sealed record ProxyGrant(Guid Id, Guid PatientId, Guid AccountId, string Scope,
    DateOnly ExpiresOn, string Status, string GrantedBy, DateTimeOffset GrantedAt,
    string RevokedBy, DateTimeOffset? RevokedAt, long Version = 1);

public sealed record DeviceSession(Guid Id, Guid AccountId, string Device, string TokenHash,
    DateTimeOffset IssuedAt, DateTimeOffset ExpiresAt, string Status);

public sealed record PortalPreference(Guid Id, Guid AccountId, string ReminderChannel,
    string Language, bool PaperlessBilling, long Version = 1);

// ── Management dashboards and reporting (§26) ────────────────────────────────────────
public sealed record ReportDefinition(Guid Id, string Code, string Name, string Category, string Status,
    int CurrentVersion, string Owner, DateTimeOffset CreatedAt, long Version = 1);

public sealed record ReportVersion(Guid Id, Guid DefinitionId, int Number, string QueryKey,
    string Columns, string Filters, string CreatedBy, DateTimeOffset CreatedAt,
    string ApprovedBy, DateTimeOffset? ApprovedAt);

public sealed record ReportExecution(Guid Id, string Number, Guid DefinitionId, int DefinitionVersion,
    string Parameters, string Rows, int RowCount, string Status, string ExecutedBy, DateTimeOffset ExecutedAt,
    string Error, long Version = 1);

public sealed record ReportExport(Guid Id, Guid ExecutionId, string Format, string Status,
    string Watermark, string FileRef, string RequestedBy, DateTimeOffset RequestedAt);

public sealed record ReportSubscription(Guid Id, Guid DefinitionId, string Cadence, string Recipients,
    bool Active, string CreatedBy, DateTimeOffset CreatedAt, long Version = 1);

public sealed record DashboardCard(string Label, string Value, string Detail, string Freshness, bool Provisional);
