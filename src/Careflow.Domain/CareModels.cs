namespace Careflow.Domain;

public sealed record Bed(Guid Id, string Ward, string Number, string Kind, string Status, string Notes, long Version = 1);
public sealed record Admission(Guid Id, string Number, Guid PatientId, Guid BedId, Guid DoctorId, string Status, string Reason,
    DateTimeOffset AdmittedAt, DateTimeOffset? DischargedAt, string DischargeSummary, string DischargeDiagnosis, string DischargeInstructions, long Version = 1);
public sealed record BedEvent(Guid Id, Guid? AdmissionId, Guid BedId, string Action, string FromStatus, string ToStatus, Guid ActorId, DateTimeOffset At, string Reason);
public sealed record NursingNote(Guid Id, Guid AdmissionId, Guid PatientId, string Kind, string Text, VitalSet Vitals,
    int? IntakeMl, int? OutputMl, int? PainScore, string FallRisk, Guid CreatedBy, DateTimeOffset CreatedAt);
public sealed record MedicationOrder(Guid Id, Guid AdmissionId, Guid PatientId, Guid MedicineId, string Name, string Dose, string Route,
    string Frequency, DateTimeOffset StartsAt, DateTimeOffset? EndsAt, string Status, Guid OrderedBy, DateTimeOffset CreatedAt, long Version = 1);
public sealed record MedicationAdministration(Guid Id, Guid OrderId, Guid AdmissionId, Guid BatchId, decimal Quantity,
    DateTimeOffset GivenAt, Guid GivenBy, string Witness, string Notes);
public sealed record ShiftHandover(Guid Id, Guid FromNurse, string ToNurseName, string Summary, IReadOnlyList<Guid> PatientIds,
    string Status, DateTimeOffset CreatedAt, DateTimeOffset? AcknowledgedAt, Guid? AcknowledgedBy, long Version = 1)
{
    public Guid ToNurseId { get; init; }
}
public sealed record StaffMember(Guid Id, string Name);
public sealed record NursingEscalationPolicy(int Spo2Below, int SystolicBelow, int SystolicAbove, int PulseBelow, int PulseAbove, decimal TemperatureAtOrAbove, int PainAtOrAbove);
public sealed record StockBatch(Guid Id, Guid MedicineId, string BatchNumber, DateOnly ExpiryDate, decimal Quantity, decimal UnitCost,
    string Supplier, string Reference, DateTimeOffset ReceivedAt, Guid ReceivedBy, string Status = "Active", long Version = 1);
public sealed record StockMovement(Guid Id, Guid BatchId, string Kind, decimal Quantity, string Reference, Guid ActorId, DateTimeOffset At);
public sealed record BatchAllocation(Guid BatchId, string BatchNumber, decimal Quantity);
public sealed record DispenseLine(Guid MedicineId, string Name, decimal Quantity, IReadOnlyList<BatchAllocation> Batches);
public sealed record Dispense(Guid Id, string Number, string Kind, Guid PatientId, Guid? EncounterId, Guid? AdmissionId,
    IReadOnlyList<DispenseLine> Lines, string IdempotencyKey, DateTimeOffset CreatedAt, Guid CreatedBy)
{
    public string RequestHash { get; init; } = "";
}
public sealed record LabTest(Guid Id, string Code, string Name, string SpecimenType, string Unit,
    decimal? ReferenceLow, decimal? ReferenceHigh, decimal? CriticalLow, decimal? CriticalHigh, bool Active = true);
public sealed record LabOrder(Guid Id, string Number, Guid PatientId, Guid? EncounterId, Guid? AdmissionId, Guid TestId, string Priority,
    string Status, string? Barcode, string? Accession, Guid OrderedBy, DateTimeOffset OrderedAt, DateTimeOffset? CollectedAt,
    DateTimeOffset? ProcessedAt, DateTimeOffset? ResultedAt, DateTimeOffset? ValidatedAt, DateTimeOffset? AuthorizedAt,
    string RejectionReason, long Version = 1)
{
    public string ClinicalNote { get; init; } = "";
    public string? SpecimenType { get; init; }
    public decimal? VolumeMl { get; init; }
    public Guid? CollectedBy { get; init; }
}
public sealed record LabResult(Guid Id, Guid OrderId, int Version, string Value, decimal? NumericValue, string Unit,
    decimal? ReferenceLow, decimal? ReferenceHigh, string Flag, Guid EnteredBy, DateTimeOffset EnteredAt,
    Guid? ValidatedBy, DateTimeOffset? ValidatedAt, Guid? AuthorizedBy, DateTimeOffset? AuthorizedAt, string? ContentHash);
public sealed record CriticalAlert(Guid Id, string Kind, Guid EntityId, Guid PatientId, string Message, string Severity,
    string Status, DateTimeOffset CreatedAt, DateTimeOffset? AcknowledgedAt, Guid? AcknowledgedBy, string AcknowledgeNote);
public sealed record CareWorkspace(
    IReadOnlyList<Bed> Beds, IReadOnlyList<Admission> Admissions, IReadOnlyList<BedEvent> BedEvents,
    IReadOnlyList<NursingNote> NursingNotes, IReadOnlyList<MedicationOrder> MedicationOrders,
    IReadOnlyList<MedicationAdministration> Administrations, IReadOnlyList<ShiftHandover> Handovers,
    IReadOnlyList<StockBatch> StockBatches, IReadOnlyList<StockMovement> StockMovements, IReadOnlyList<Dispense> Dispenses,
    IReadOnlyList<LabTest> LabTests, IReadOnlyList<LabOrder> LabOrders, IReadOnlyList<LabResult> LabResults,
    IReadOnlyList<CriticalAlert> CriticalAlerts, bool Truncated = false)
{
    public IReadOnlyList<StaffMember> Nurses { get; init; } = [];
    public IReadOnlyList<RadiologyOrder> RadiologyOrders { get; init; } = [];
    public IReadOnlyList<RadiologyReport> RadiologyReports { get; init; } = [];
    public IReadOnlyList<EmergencyRegistration> EmergencyRegistrations { get; init; } = [];
    public IReadOnlyList<TriageAssessment> TriageAssessments { get; init; } = [];
    public IReadOnlyList<OtBooking> OtBookings { get; init; } = [];
    public IReadOnlyList<BloodUnit> BloodUnits { get; init; } = [];
    public IReadOnlyList<BloodIssue> BloodIssues { get; init; } = [];
    public IReadOnlyList<Payer> Payers { get; init; } = [];
    public IReadOnlyList<InsurancePlan> Plans { get; init; } = [];
    public IReadOnlyList<Coverage> Coverages { get; init; } = [];
    public IReadOnlyList<Preauthorization> Preauthorizations { get; init; } = [];
    public IReadOnlyList<Claim> Claims { get; init; } = [];
    public IReadOnlyList<ClaimLine> ClaimLines { get; init; } = [];
    public IReadOnlyList<Settlement> Settlements { get; init; } = [];
    public IReadOnlyList<IncentiveRule> IncentiveRules { get; init; } = [];
    public IReadOnlyList<IncentiveBatch> IncentiveBatches { get; init; } = [];
    public IReadOnlyList<IncentiveLine> IncentiveLines { get; init; } = [];
    public IReadOnlyList<IncentivePayment> IncentivePayments { get; init; } = [];
    public IReadOnlyList<TeleSession> TeleSessions { get; init; } = [];
    public IReadOnlyList<InventoryItem> InventoryItems { get; init; } = [];
    public IReadOnlyList<InventoryStore> InventoryStores { get; init; } = [];
    public IReadOnlyList<Vendor> Vendors { get; init; } = [];
    public IReadOnlyList<InventoryBatch> InventoryBatches { get; init; } = [];
    public IReadOnlyList<InventoryIndent> InventoryIndents { get; init; } = [];
    public IReadOnlyList<PurchaseOrder> PurchaseOrders { get; init; } = [];
    public IReadOnlyList<InventoryTransfer> Transfers { get; init; } = [];
    public IReadOnlyList<InstrumentSet> CssdSets { get; init; } = [];
    public IReadOnlyList<SterilizerCycle> CssdCycles { get; init; } = [];
    public IReadOnlyList<CssdIssue> CssdIssues { get; init; } = [];
    public IReadOnlyList<Infection> Infections { get; init; } = [];
    public IReadOnlyList<Incident> Incidents { get; init; } = [];
    public IReadOnlyList<RiskAssessment> Risks { get; init; } = [];
    public IReadOnlyList<Capa> Capas { get; init; } = [];
    public IReadOnlyList<ClinicalAudit> Audits { get; init; } = [];
    public IReadOnlyList<QualityIndicator> QualityIndicators { get; init; } = [];
    public IReadOnlyList<IndicatorResult> IndicatorResults { get; init; } = [];
    public IReadOnlyList<QualityDocument> QualityDocuments { get; init; } = [];
    public IReadOnlyList<PortalAccount> PortalAccounts { get; init; } = [];
    public IReadOnlyList<ProxyGrant> ProxyGrants { get; init; } = [];
    public IReadOnlyList<ReportDefinition> ReportDefinitions { get; init; } = [];
    public IReadOnlyList<ReportExecution> ReportExecutions { get; init; } = [];
    public static CareWorkspace Empty { get; } = new([], [], [], [], [], [], [], [], [], [], [], [], [], []);
}
public sealed record CareChart(IReadOnlyList<Admission> Admissions, IReadOnlyList<NursingNote> NursingNotes,
    IReadOnlyList<MedicationOrder> MedicationOrders, IReadOnlyList<Dispense> Dispenses, IReadOnlyList<LabOrder> LabOrders,
    IReadOnlyList<LabResult> LabResults, bool Truncated = false)
{
    public IReadOnlyList<RadiologyOrder> RadiologyOrders { get; init; } = [];
    public IReadOnlyList<RadiologyReport> RadiologyReports { get; init; } = [];
    public IReadOnlyList<EmergencyRegistration> EmergencyRegistrations { get; init; } = [];
    public IReadOnlyList<OtBooking> OtBookings { get; init; } = [];
    public IReadOnlyList<BloodIssue> BloodIssues { get; init; } = [];
    public IReadOnlyList<Coverage> Coverages { get; init; } = [];
    public IReadOnlyList<Preauthorization> Preauthorizations { get; init; } = [];
    public IReadOnlyList<Claim> Claims { get; init; } = [];
    public IReadOnlyList<TeleSession> TeleSessions { get; init; } = [];
    public static CareChart Empty { get; } = new([], [], [], [], [], []);
}
