using Careflow.Domain;
namespace Careflow.Application;

// Phase 5 inputs (quality/accreditation, patient portal, management reporting).
public sealed record InfectionInput(Guid PatientId, Guid? EncounterId, string Category, string Site,
    string DetectedMethod, DateOnly DetectedOn, string Severity);
public sealed record InfectionInvestigateInput(string Investigation, string Outcome, string Status, long Version);

public sealed record IncidentInput(string Category, string Severity, string Description, string Location, Guid? PatientId);
public sealed record IncidentTriageInput(string TriageNote, string AssignedTo, long Version);
public sealed record IncidentCloseInput(string Investigation, string RootCause, long Version);

public sealed record RiskInput(string Domain, string Description, int Probability, int Impact,
    string Mitigation, string Owner);
public sealed record RiskUpdateInput(string Mitigation, string Owner, string Status, long Version);

public sealed record CapaInput(Guid? IncidentId, Guid? InfectionId, Guid? AuditId, Guid? RiskId,
    string Correction, string CorrectiveAction, string PreventiveAction, string Owner, DateTimeOffset DueDate);
public sealed record CapaProgressInput(string CorrectiveAction, string PreventiveAction, long Version);
public sealed record CapaVerifyInput(string EffectivenessEvidence, long Version);
public sealed record CapaCloseInput(string Reason, long Version);

public sealed record AuditInput(string Domain, string Scope, DateOnly PlannedFor, string Lead);
public sealed record AuditFindingsInput(string Findings, int ScorePercent, long Version);
public sealed record AuditReportInput(string Report, long Version);

public sealed record QualityIndicatorInput(string Code, string Name, string Definition, string Numerator,
    string Denominator, string Period, string Owner);
public sealed record QualityIndicatorResultInput(string PeriodStart, string PeriodEnd, decimal Numerator,
    decimal Denominator, string Basis, string Note);

public sealed record DocumentInput(string Title, string Category, string Owner, string Summary, string Content);
public sealed record DocumentVersionInput(string Summary, string Content);
public sealed record DocumentTransitionInput(string Reason, long Version);
public sealed record DocumentAcknowledgeInput(string Attestation);

public sealed record PortalRegisterInput(Guid PatientId, Guid UserId, string UserName, string ProofingEvidence);
public sealed record PortalVerifyInput(string VerifiedBy, long Version);
public sealed record PortalLinkInput(Guid PatientId, string Relationship, string ProofingEvidence);
public sealed record ProxyGrantInput(Guid AccountId, string Scope, DateOnly ExpiresOn);
public sealed record ProxyRevokeInput(string Reason, long Version);
public sealed record PortalLoginInput(string Device);
public sealed record PortalPreferenceInput(string ReminderChannel, string Language, bool PaperlessBilling);
public sealed record PortalAppointmentInput(Guid DoctorId, DateTimeOffset StartsAt, int DurationMinutes, string Reason);
public sealed record PortalPaymentInput(Guid InvoiceId, decimal Amount, string Method, string Reference);
public sealed record PortalConsentInput(string Kind, bool Given, string Note);

public sealed record ReportDefinitionInput(string Code, string Name, string Category, string Owner,
    string QueryKey, string Columns, string Filters);
public sealed record ReportExecuteInput(Guid DefinitionId, string Parameters);
public sealed record ReportExportInput(Guid ExecutionId, string Format);
public sealed record SubscriptionInput(Guid DefinitionId, string Cadence, string Recipients);

public partial interface IHospitalStore
{
    // Quality (§24)
    Task<Infection?> FindInfectionAsync(Guid id, CancellationToken ct);
    Task InsertInfectionAsync(Infection infection, CancellationToken ct);
    Task UpdateInfectionAsync(Infection infection, CancellationToken ct);
    Task<IReadOnlyList<Infection>> ListInfectionsAsync(Guid? patientId, CancellationToken ct);
    Task<Incident?> FindIncidentAsync(Guid id, CancellationToken ct);
    Task InsertIncidentAsync(Incident incident, CancellationToken ct);
    Task UpdateIncidentAsync(Incident incident, CancellationToken ct);
    Task<IReadOnlyList<Incident>> ListIncidentsAsync(CancellationToken ct);
    Task<RiskAssessment?> FindRiskAsync(Guid id, CancellationToken ct);
    Task InsertRiskAsync(RiskAssessment risk, CancellationToken ct);
    Task UpdateRiskAsync(RiskAssessment risk, CancellationToken ct);
    Task<IReadOnlyList<RiskAssessment>> ListRisksAsync(CancellationToken ct);
    Task<Capa?> FindCapaAsync(Guid id, CancellationToken ct);
    Task InsertCapaAsync(Capa capa, CancellationToken ct);
    Task UpdateCapaAsync(Capa capa, CancellationToken ct);
    Task<IReadOnlyList<Capa>> ListCapasAsync(CancellationToken ct);
    Task<ClinicalAudit?> FindAuditAsync(Guid id, CancellationToken ct);
    Task InsertAuditAsync(ClinicalAudit audit, CancellationToken ct);
    Task UpdateAuditAsync(ClinicalAudit audit, CancellationToken ct);
    Task<IReadOnlyList<ClinicalAudit>> ListAuditsAsync(CancellationToken ct);
    Task<QualityIndicator?> FindIndicatorAsync(Guid id, CancellationToken ct);
    Task<QualityIndicator?> FindIndicatorByCodeAsync(string code, CancellationToken ct);
    Task InsertIndicatorAsync(QualityIndicator indicator, CancellationToken ct);
    Task UpdateIndicatorAsync(QualityIndicator indicator, CancellationToken ct);
    Task<IReadOnlyList<QualityIndicator>> ListIndicatorsAsync(CancellationToken ct);
    Task InsertIndicatorResultAsync(IndicatorResult result, CancellationToken ct);
    Task<IReadOnlyList<IndicatorResult>> ListIndicatorResultsAsync(Guid? indicatorId, CancellationToken ct);
    Task<QualityDocument?> FindQualityDocumentAsync(Guid id, CancellationToken ct);
    Task InsertQualityDocumentAsync(QualityDocument document, CancellationToken ct);
    Task UpdateQualityDocumentAsync(QualityDocument document, CancellationToken ct);
    Task<IReadOnlyList<QualityDocument>> ListQualityDocumentsAsync(CancellationToken ct);
    Task InsertDocumentVersionAsync(DocumentVersion version, CancellationToken ct);
    Task<IReadOnlyList<DocumentVersion>> ListDocumentVersionsAsync(Guid documentId, CancellationToken ct);
    Task<DocumentVersion?> FindDocumentVersionAsync(Guid id, CancellationToken ct);
    Task UpdateDocumentVersionAsync(DocumentVersion version, CancellationToken ct);
    Task InsertSopAcknowledgmentAsync(SopAcknowledgment ack, CancellationToken ct);
    Task<IReadOnlyList<SopAcknowledgment>> ListSopAcknowledgmentsAsync(Guid documentId, CancellationToken ct);

    // Patient portal (§25)
    Task<PortalAccount?> FindPortalAccountAsync(Guid id, CancellationToken ct);
    Task<PortalAccount?> FindPortalAccountByUserIdAsync(Guid userId, CancellationToken ct);
    Task<PortalAccount?> FindPortalAccountByPatientAsync(Guid patientId, CancellationToken ct);
    Task InsertPortalAccountAsync(PortalAccount account, CancellationToken ct);
    Task UpdatePortalAccountAsync(PortalAccount account, CancellationToken ct);
    Task<IReadOnlyList<PortalAccount>> ListPortalAccountsAsync(CancellationToken ct);
    Task<PatientLink?> FindPatientLinkAsync(Guid accountId, Guid patientId, CancellationToken ct);
    Task InsertPatientLinkAsync(PatientLink link, CancellationToken ct);
    Task UpdatePatientLinkAsync(PatientLink link, CancellationToken ct);
    Task<IReadOnlyList<PatientLink>> ListPatientLinksAsync(Guid accountId, CancellationToken ct);
    Task<ProxyGrant?> FindProxyGrantAsync(Guid id, CancellationToken ct);
    Task<ProxyGrant?> FindActiveProxyGrantAsync(Guid patientId, Guid accountId, string scope, DateOnly on, CancellationToken ct);
    Task InsertProxyGrantAsync(ProxyGrant grant, CancellationToken ct);
    Task UpdateProxyGrantAsync(ProxyGrant grant, CancellationToken ct);
    Task<IReadOnlyList<ProxyGrant>> ListProxyGrantsAsync(Guid? patientId, CancellationToken ct);
    Task InsertDeviceSessionAsync(DeviceSession session, CancellationToken ct);
    Task<IReadOnlyList<DeviceSession>> ListDeviceSessionsAsync(Guid accountId, CancellationToken ct);
    Task<PortalPreference?> FindPortalPreferenceAsync(Guid accountId, CancellationToken ct);
    Task InsertPortalPreferenceAsync(PortalPreference preference, CancellationToken ct);
    Task UpdatePortalPreferenceAsync(PortalPreference preference, CancellationToken ct);
    Task<IReadOnlyList<LabOrder>> ListPortalLabOrdersAsync(Guid patientId, CancellationToken ct);
    Task<IReadOnlyList<LabResult>> ListPortalLabResultsAsync(Guid orderId, CancellationToken ct);
    Task<IReadOnlyList<RadiologyReport>> ListPortalRadiologyReportsAsync(Guid patientId, CancellationToken ct);
    Task<IReadOnlyList<LabTest>> ListLabTestsAsync(CancellationToken ct);
    Task<Chart?> LoadPortalChartAsync(Guid patientId, int limit, CancellationToken ct);

    // Reporting (§26)
    Task<ReportDefinition?> FindReportDefinitionAsync(Guid id, CancellationToken ct);
    Task<ReportDefinition?> FindReportDefinitionByCodeAsync(string code, CancellationToken ct);
    Task InsertReportDefinitionAsync(ReportDefinition definition, CancellationToken ct);
    Task UpdateReportDefinitionAsync(ReportDefinition definition, CancellationToken ct);
    Task<IReadOnlyList<ReportDefinition>> ListReportDefinitionsAsync(CancellationToken ct);
    Task InsertReportVersionAsync(ReportVersion version, CancellationToken ct);
    Task<IReadOnlyList<ReportVersion>> ListReportVersionsAsync(Guid definitionId, CancellationToken ct);
    Task<ReportExecution?> FindReportExecutionAsync(Guid id, CancellationToken ct);
    Task InsertReportExecutionAsync(ReportExecution execution, CancellationToken ct);
    Task UpdateReportExecutionAsync(ReportExecution execution, CancellationToken ct);
    Task<IReadOnlyList<ReportExecution>> ListReportExecutionsAsync(CancellationToken ct);
    Task<ReportExport?> FindReportExportAsync(Guid id, CancellationToken ct);
    Task InsertReportExportAsync(ReportExport export, CancellationToken ct);
    Task UpdateReportExportAsync(ReportExport export, CancellationToken ct);
    Task<IReadOnlyList<ReportExport>> ListReportExportsAsync(Guid? executionId, CancellationToken ct);
    Task<ReportSubscription?> FindReportSubscriptionAsync(Guid id, CancellationToken ct);
    Task InsertReportSubscriptionAsync(ReportSubscription subscription, CancellationToken ct);
    Task UpdateReportSubscriptionAsync(ReportSubscription subscription, CancellationToken ct);
    Task<IReadOnlyList<ReportSubscription>> ListReportSubscriptionsAsync(CancellationToken ct);
    Task<IReadOnlyList<Patient>> ListPatientsBriefAsync(CancellationToken ct);
    Task<IReadOnlyList<Appointment>> ListAppointmentsBriefAsync(CancellationToken ct);
    Task<IReadOnlyList<Encounter>> ListEncountersBriefAsync(CancellationToken ct);
    Task<IReadOnlyList<Invoice>> ListInvoicesBriefAsync(CancellationToken ct);
}
