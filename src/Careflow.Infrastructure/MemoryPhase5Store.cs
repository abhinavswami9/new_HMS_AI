using Careflow.Application;
using Careflow.Domain;
namespace Careflow.Infrastructure;

// Partial MemoryHospitalStore slice for the Phase 5 packages: quality/accreditation,
// patient portal and management reporting.

public sealed partial class MemoryHospitalStore
{
    static readonly string[] ClosedQualityStates = ["Closed", "Cancelled", "Retired"];

    // ---- Phase 5: quality ---------------------------------------------------
    public Task<Infection?> FindInfectionAsync(Guid id, CancellationToken ct) => Task.FromResult(data.Infections.FirstOrDefault(x => x.Id == id));
    public Task InsertInfectionAsync(Infection infection, CancellationToken ct) { data.Infections.Add(infection); return Task.CompletedTask; }
    public Task UpdateInfectionAsync(Infection infection, CancellationToken ct) { var old = data.Infections.FirstOrDefault(x => x.Id == infection.Id) ?? throw new DomainException("not_found", "Infection record not found.", 404); CareVersion(old.Version, infection.Version); data.Infections[data.Infections.IndexOf(old)] = infection; return Task.CompletedTask; }
    public Task<IReadOnlyList<Infection>> ListInfectionsAsync(Guid? patientId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Infection>>(data.Infections.Where(x => patientId is null || x.PatientId == patientId).ToList());

    public Task<Incident?> FindIncidentAsync(Guid id, CancellationToken ct) => Task.FromResult(data.Incidents.FirstOrDefault(x => x.Id == id));
    public Task InsertIncidentAsync(Incident incident, CancellationToken ct) { data.Incidents.Add(incident); return Task.CompletedTask; }
    public Task UpdateIncidentAsync(Incident incident, CancellationToken ct) { var old = data.Incidents.FirstOrDefault(x => x.Id == incident.Id) ?? throw new DomainException("not_found", "Incident not found.", 404); CareVersion(old.Version, incident.Version); data.Incidents[data.Incidents.IndexOf(old)] = incident; return Task.CompletedTask; }
    public Task<IReadOnlyList<Incident>> ListIncidentsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<Incident>>(data.Incidents.ToList());

    public Task<RiskAssessment?> FindRiskAsync(Guid id, CancellationToken ct) => Task.FromResult(data.Risks.FirstOrDefault(x => x.Id == id));
    public Task InsertRiskAsync(RiskAssessment risk, CancellationToken ct) { data.Risks.Add(risk); return Task.CompletedTask; }
    public Task UpdateRiskAsync(RiskAssessment risk, CancellationToken ct) { var old = data.Risks.FirstOrDefault(x => x.Id == risk.Id) ?? throw new DomainException("not_found", "Risk not found.", 404); CareVersion(old.Version, risk.Version); data.Risks[data.Risks.IndexOf(old)] = risk; return Task.CompletedTask; }
    public Task<IReadOnlyList<RiskAssessment>> ListRisksAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<RiskAssessment>>(data.Risks.ToList());

    public Task<Capa?> FindCapaAsync(Guid id, CancellationToken ct) => Task.FromResult(data.Capas.FirstOrDefault(x => x.Id == id));
    public Task InsertCapaAsync(Capa capa, CancellationToken ct) { data.Capas.Add(capa); return Task.CompletedTask; }
    public Task UpdateCapaAsync(Capa capa, CancellationToken ct) { var old = data.Capas.FirstOrDefault(x => x.Id == capa.Id) ?? throw new DomainException("not_found", "CAPA not found.", 404); CareVersion(old.Version, capa.Version); data.Capas[data.Capas.IndexOf(old)] = capa; return Task.CompletedTask; }
    public Task<IReadOnlyList<Capa>> ListCapasAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<Capa>>(data.Capas.ToList());

    public Task<ClinicalAudit?> FindAuditAsync(Guid id, CancellationToken ct) => Task.FromResult(data.Audits.FirstOrDefault(x => x.Id == id));
    public Task InsertAuditAsync(ClinicalAudit audit, CancellationToken ct) { data.Audits.Add(audit); return Task.CompletedTask; }
    public Task UpdateAuditAsync(ClinicalAudit audit, CancellationToken ct) { var old = data.Audits.FirstOrDefault(x => x.Id == audit.Id) ?? throw new DomainException("not_found", "Audit not found.", 404); CareVersion(old.Version, audit.Version); data.Audits[data.Audits.IndexOf(old)] = audit; return Task.CompletedTask; }
    public Task<IReadOnlyList<ClinicalAudit>> ListAuditsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<ClinicalAudit>>(data.Audits.ToList());

    public Task<QualityIndicator?> FindIndicatorAsync(Guid id, CancellationToken ct) => Task.FromResult(data.QualityIndicators.FirstOrDefault(x => x.Id == id));
    public Task<QualityIndicator?> FindIndicatorByCodeAsync(string code, CancellationToken ct) => Task.FromResult(data.QualityIndicators.FirstOrDefault(x => x.Code == code));
    public Task InsertIndicatorAsync(QualityIndicator indicator, CancellationToken ct) { data.QualityIndicators.Add(indicator); return Task.CompletedTask; }
    public Task UpdateIndicatorAsync(QualityIndicator indicator, CancellationToken ct) { var old = data.QualityIndicators.FirstOrDefault(x => x.Id == indicator.Id) ?? throw new DomainException("not_found", "Indicator not found.", 404); data.QualityIndicators[data.QualityIndicators.IndexOf(old)] = indicator; return Task.CompletedTask; }
    public Task<IReadOnlyList<QualityIndicator>> ListIndicatorsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<QualityIndicator>>(data.QualityIndicators.ToList());
    public Task InsertIndicatorResultAsync(IndicatorResult result, CancellationToken ct) { data.IndicatorResults.Add(result); return Task.CompletedTask; }
    public Task<IReadOnlyList<IndicatorResult>> ListIndicatorResultsAsync(Guid? indicatorId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<IndicatorResult>>(data.IndicatorResults.Where(x => indicatorId is null || x.IndicatorId == indicatorId).ToList());

    public Task<QualityDocument?> FindQualityDocumentAsync(Guid id, CancellationToken ct) => Task.FromResult(data.QualityDocuments.FirstOrDefault(x => x.Id == id));
    public Task InsertQualityDocumentAsync(QualityDocument document, CancellationToken ct) { data.QualityDocuments.Add(document); return Task.CompletedTask; }
    public Task UpdateQualityDocumentAsync(QualityDocument document, CancellationToken ct) { var old = data.QualityDocuments.FirstOrDefault(x => x.Id == document.Id) ?? throw new DomainException("not_found", "Document not found.", 404); CareVersion(old.Version, document.Version); data.QualityDocuments[data.QualityDocuments.IndexOf(old)] = document; return Task.CompletedTask; }
    public Task<IReadOnlyList<QualityDocument>> ListQualityDocumentsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<QualityDocument>>(data.QualityDocuments.ToList());
    public Task InsertDocumentVersionAsync(DocumentVersion version, CancellationToken ct) { data.DocumentVersions.Add(version); return Task.CompletedTask; }
    public Task UpdateDocumentVersionAsync(DocumentVersion version, CancellationToken ct) { var old = data.DocumentVersions.FirstOrDefault(x => x.Id == version.Id) ?? throw new DomainException("not_found", "Document version not found.", 404); data.DocumentVersions[data.DocumentVersions.IndexOf(old)] = version; return Task.CompletedTask; }
    public Task<IReadOnlyList<DocumentVersion>> ListDocumentVersionsAsync(Guid documentId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<DocumentVersion>>(data.DocumentVersions.Where(x => x.DocumentId == documentId).ToList());
    public Task<DocumentVersion?> FindDocumentVersionAsync(Guid id, CancellationToken ct) => Task.FromResult(data.DocumentVersions.FirstOrDefault(x => x.Id == id));
    public Task InsertSopAcknowledgmentAsync(SopAcknowledgment ack, CancellationToken ct) { data.SopAcknowledgments.Add(ack); return Task.CompletedTask; }
    public Task<IReadOnlyList<SopAcknowledgment>> ListSopAcknowledgmentsAsync(Guid documentId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<SopAcknowledgment>>(data.SopAcknowledgments.Where(x => x.DocumentId == documentId).ToList());

    // ---- Phase 5: patient portal -------------------------------------------
    public Task<PortalAccount?> FindPortalAccountAsync(Guid id, CancellationToken ct) => Task.FromResult(data.PortalAccounts.FirstOrDefault(x => x.Id == id));
    public Task<PortalAccount?> FindPortalAccountByUserIdAsync(Guid userId, CancellationToken ct) => Task.FromResult(data.PortalAccounts.FirstOrDefault(x => x.UserId == userId));
    public Task<PortalAccount?> FindPortalAccountByPatientAsync(Guid patientId, CancellationToken ct) => Task.FromResult(data.PortalAccounts.FirstOrDefault(x => x.PatientId == patientId));
    public Task InsertPortalAccountAsync(PortalAccount account, CancellationToken ct) { data.PortalAccounts.Add(account); return Task.CompletedTask; }
    public Task UpdatePortalAccountAsync(PortalAccount account, CancellationToken ct) { var old = data.PortalAccounts.FirstOrDefault(x => x.Id == account.Id) ?? throw new DomainException("not_found", "Portal account not found.", 404); CareVersion(old.Version, account.Version); data.PortalAccounts[data.PortalAccounts.IndexOf(old)] = account; return Task.CompletedTask; }
    public Task<IReadOnlyList<PortalAccount>> ListPortalAccountsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<PortalAccount>>(data.PortalAccounts.ToList());
    public Task<PatientLink?> FindPatientLinkAsync(Guid accountId, Guid patientId, CancellationToken ct) =>
        Task.FromResult(data.PatientLinks.FirstOrDefault(x => x.AccountId == accountId && x.PatientId == patientId));
    public Task InsertPatientLinkAsync(PatientLink link, CancellationToken ct) { data.PatientLinks.Add(link); return Task.CompletedTask; }
    public Task UpdatePatientLinkAsync(PatientLink link, CancellationToken ct) { var old = data.PatientLinks.FirstOrDefault(x => x.Id == link.Id) ?? throw new DomainException("not_found", "Patient link not found.", 404); data.PatientLinks[data.PatientLinks.IndexOf(old)] = link; return Task.CompletedTask; }
    public Task<IReadOnlyList<PatientLink>> ListPatientLinksAsync(Guid accountId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<PatientLink>>(data.PatientLinks.Where(x => x.AccountId == accountId).ToList());
    public Task<ProxyGrant?> FindProxyGrantAsync(Guid id, CancellationToken ct) => Task.FromResult(data.ProxyGrants.FirstOrDefault(x => x.Id == id));
    public Task<ProxyGrant?> FindActiveProxyGrantAsync(Guid patientId, Guid accountId, string scope, DateOnly on, CancellationToken ct) =>
        Task.FromResult(data.ProxyGrants.FirstOrDefault(x => x.PatientId == patientId && x.AccountId == accountId && x.Scope == scope && x.Status == "Active" && x.ExpiresOn >= on));
    public Task InsertProxyGrantAsync(ProxyGrant grant, CancellationToken ct) { data.ProxyGrants.Add(grant); return Task.CompletedTask; }
    public Task UpdateProxyGrantAsync(ProxyGrant grant, CancellationToken ct) { var old = data.ProxyGrants.FirstOrDefault(x => x.Id == grant.Id) ?? throw new DomainException("not_found", "Proxy grant not found.", 404); CareVersion(old.Version, grant.Version); data.ProxyGrants[data.ProxyGrants.IndexOf(old)] = grant; return Task.CompletedTask; }
    public Task<IReadOnlyList<ProxyGrant>> ListProxyGrantsAsync(Guid? patientId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ProxyGrant>>(data.ProxyGrants.Where(x => patientId is null || x.PatientId == patientId).ToList());
    public Task InsertDeviceSessionAsync(DeviceSession session, CancellationToken ct) { data.DeviceSessions.Add(session); return Task.CompletedTask; }
    public Task<IReadOnlyList<DeviceSession>> ListDeviceSessionsAsync(Guid accountId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<DeviceSession>>(data.DeviceSessions.Where(x => x.AccountId == accountId).ToList());
    public Task<PortalPreference?> FindPortalPreferenceAsync(Guid accountId, CancellationToken ct) =>
        Task.FromResult(data.PortalPreferences.FirstOrDefault(x => x.AccountId == accountId));
    public Task InsertPortalPreferenceAsync(PortalPreference preference, CancellationToken ct) { data.PortalPreferences.Add(preference); return Task.CompletedTask; }
    public Task UpdatePortalPreferenceAsync(PortalPreference preference, CancellationToken ct) { var old = data.PortalPreferences.FirstOrDefault(x => x.Id == preference.Id) ?? throw new DomainException("not_found", "Portal preferences not found.", 404); CareVersion(old.Version, preference.Version); data.PortalPreferences[data.PortalPreferences.IndexOf(old)] = preference; return Task.CompletedTask; }
    public Task<IReadOnlyList<LabOrder>> ListPortalLabOrdersAsync(Guid patientId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<LabOrder>>(data.LabOrders.Where(x => x.PatientId == patientId).ToList());
    public Task<IReadOnlyList<LabResult>> ListPortalLabResultsAsync(Guid orderId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<LabResult>>(data.LabResults.Where(x => x.OrderId == orderId).ToList());
    public Task<IReadOnlyList<RadiologyReport>> ListPortalRadiologyReportsAsync(Guid patientId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<RadiologyReport>>(data.RadiologyReports.Where(r => data.RadiologyOrders.Any(o => o.Id == r.OrderId && o.PatientId == patientId)).ToList());
    public Task<IReadOnlyList<LabTest>> ListLabTestsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<LabTest>>(data.LabTests.ToList());

    public Task<Chart?> LoadPortalChartAsync(Guid patientId, int limit, CancellationToken ct)
    {
        var patient = data.Patients.FirstOrDefault(p => p.Id == patientId); if (patient is null) return Task.FromResult<Chart?>(null);
        var encounters = data.Encounters.Where(e => e.PatientId == patientId).OrderByDescending(e => e.UpdatedAt).ToList();
        var appointments = data.Appointments.Where(a => a.PatientId == patientId).OrderByDescending(a => a.StartsAt).ToList();
        var invoices = data.Invoices.Where(i => i.PatientId == patientId).OrderByDescending(i => i.CreatedAt).ToList();
        return Task.FromResult<Chart?>(new Chart(patient, encounters.Take(limit).ToList(), appointments.Take(limit).ToList(), invoices.Take(limit).ToList(), new[] { encounters.Count, appointments.Count, invoices.Count }.Any(n => n > limit)));
    }

    // ---- Phase 5: reporting -------------------------------------------------
    public Task<ReportDefinition?> FindReportDefinitionAsync(Guid id, CancellationToken ct) => Task.FromResult(data.ReportDefinitions.FirstOrDefault(x => x.Id == id));
    public Task<ReportDefinition?> FindReportDefinitionByCodeAsync(string code, CancellationToken ct) => Task.FromResult(data.ReportDefinitions.FirstOrDefault(x => x.Code == code));
    public Task InsertReportDefinitionAsync(ReportDefinition definition, CancellationToken ct) { data.ReportDefinitions.Add(definition); return Task.CompletedTask; }
    public Task UpdateReportDefinitionAsync(ReportDefinition definition, CancellationToken ct) { var old = data.ReportDefinitions.FirstOrDefault(x => x.Id == definition.Id) ?? throw new DomainException("not_found", "Report definition not found.", 404); CareVersion(old.Version, definition.Version); data.ReportDefinitions[data.ReportDefinitions.IndexOf(old)] = definition; return Task.CompletedTask; }
    public Task<IReadOnlyList<ReportDefinition>> ListReportDefinitionsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<ReportDefinition>>(data.ReportDefinitions.ToList());
    public Task InsertReportVersionAsync(ReportVersion version, CancellationToken ct) { data.ReportVersions.Add(version); return Task.CompletedTask; }
    public Task<IReadOnlyList<ReportVersion>> ListReportVersionsAsync(Guid definitionId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ReportVersion>>(data.ReportVersions.Where(x => x.DefinitionId == definitionId).ToList());
    public Task<ReportExecution?> FindReportExecutionAsync(Guid id, CancellationToken ct) => Task.FromResult(data.ReportExecutions.FirstOrDefault(x => x.Id == id));
    public Task InsertReportExecutionAsync(ReportExecution execution, CancellationToken ct) { data.ReportExecutions.Add(execution); return Task.CompletedTask; }
    public Task UpdateReportExecutionAsync(ReportExecution execution, CancellationToken ct) { var old = data.ReportExecutions.FirstOrDefault(x => x.Id == execution.Id) ?? throw new DomainException("not_found", "Report execution not found.", 404); data.ReportExecutions[data.ReportExecutions.IndexOf(old)] = execution; return Task.CompletedTask; }
    public Task<IReadOnlyList<ReportExecution>> ListReportExecutionsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<ReportExecution>>(data.ReportExecutions.ToList());
    public Task<ReportExport?> FindReportExportAsync(Guid id, CancellationToken ct) => Task.FromResult(data.ReportExports.FirstOrDefault(x => x.Id == id));
    public Task InsertReportExportAsync(ReportExport export, CancellationToken ct) { data.ReportExports.Add(export); return Task.CompletedTask; }
    public Task UpdateReportExportAsync(ReportExport export, CancellationToken ct) { var old = data.ReportExports.FirstOrDefault(x => x.Id == export.Id) ?? throw new DomainException("not_found", "Report export not found.", 404); data.ReportExports[data.ReportExports.IndexOf(old)] = export; return Task.CompletedTask; }
    public Task<IReadOnlyList<ReportExport>> ListReportExportsAsync(Guid? executionId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ReportExport>>(data.ReportExports.Where(x => executionId is null || x.ExecutionId == executionId).ToList());
    public Task<ReportSubscription?> FindReportSubscriptionAsync(Guid id, CancellationToken ct) => Task.FromResult(data.ReportSubscriptions.FirstOrDefault(x => x.Id == id));
    public Task InsertReportSubscriptionAsync(ReportSubscription subscription, CancellationToken ct) { data.ReportSubscriptions.Add(subscription); return Task.CompletedTask; }
    public Task UpdateReportSubscriptionAsync(ReportSubscription subscription, CancellationToken ct) { var old = data.ReportSubscriptions.FirstOrDefault(x => x.Id == subscription.Id) ?? throw new DomainException("not_found", "Report subscription not found.", 404); CareVersion(old.Version, subscription.Version); data.ReportSubscriptions[data.ReportSubscriptions.IndexOf(old)] = subscription; return Task.CompletedTask; }
    public Task<IReadOnlyList<ReportSubscription>> ListReportSubscriptionsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<ReportSubscription>>(data.ReportSubscriptions.ToList());
    public Task<IReadOnlyList<Patient>> ListPatientsBriefAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<Patient>>(data.Patients.ToList());
    public Task<IReadOnlyList<Appointment>> ListAppointmentsBriefAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<Appointment>>(data.Appointments.ToList());
    public Task<IReadOnlyList<Encounter>> ListEncountersBriefAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<Encounter>>(data.Encounters.ToList());
    public Task<IReadOnlyList<Invoice>> ListInvoicesBriefAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<Invoice>>(data.Invoices.ToList());
}
