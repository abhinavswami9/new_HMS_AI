using Careflow.Domain;
namespace Careflow.Application;

public sealed record RadiologyOrderInput(Guid PatientId, Guid? EncounterId, Guid? AdmissionId, string Procedure,
    string Modality, string BodyPart, string Laterality, string Priority, string ClinicalNote);
public sealed record RadiologyScheduleInput(DateTimeOffset At, long Version);
public sealed record RadiologyPerformInput(string StudyUid, long Version);
public sealed record RadiologyReportInput(string Findings, string Impression, bool Critical, long Version);
public sealed record EmergencyRegistrationInput(Guid? PatientId, string PatientName, string Sex, DateOnly? DateOfBirth, string ArrivalMode)
{
    public string ReferringFacility { get; init; } = "";
    public string BroughtBy { get; init; } = "";
    public bool MedicoLegalCase { get; init; }
};
public sealed record TriageInput(string Category, string Complaint, VitalSet Vitals);
public sealed record EmergencyObservationInput(string Note, VitalSet Vitals);
public sealed record DispositionInput(string Kind, string Summary, Guid? BedId, Guid? DoctorId, string? ReceivingFacility);
public sealed record OtBookingInput(Guid PatientId, Guid? AdmissionId, string Procedure, string Laterality, string Site,
    Guid SurgeonId, string Anaesthesia, string Priority);
public sealed record OtScheduleInput(string Theatre, DateTimeOffset Start, int DurationMinutes, long Version);
public sealed record OtChecklistInput(bool Consent, bool AnaesthesiaAssessment, bool FastingConfirmed, bool SiteMarked, bool SafetyChecklist, string Notes);
public sealed record OtCompleteInput(string OperationNote, long Version);
public sealed record ReceiveUnitInput(string UnitNumber, string BloodGroup, string Component, DateOnly ExpiryDate, string DonorName);
public sealed record ScreeningInput(bool Hbsag, bool Hiv, bool Hcv, bool Vdrl, string Notes);
public sealed record CrossmatchInput(Guid UnitId, Guid PatientId);
public sealed record BloodIssueInput(Guid UnitId, Guid PatientId, Guid? AdmissionId, string Notes);
public sealed record TransfuseInput(string Reaction, string Notes, long Version);
public sealed record ReturnInput(bool AssessedFit, string Notes, long Version);

public partial interface IHospitalStore
{
    Task<RadiologyOrder?> FindRadiologyOrderAsync(Guid id, CancellationToken ct);
    Task InsertRadiologyOrderAsync(RadiologyOrder order, CancellationToken ct);
    Task UpdateRadiologyOrderAsync(RadiologyOrder order, CancellationToken ct);
    Task<RadiologyReport?> FindRadiologyReportByOrderAsync(Guid orderId, CancellationToken ct);
    Task InsertRadiologyReportAsync(RadiologyReport report, CancellationToken ct);
    Task UpdateRadiologyReportAsync(RadiologyReport report, CancellationToken ct);
    Task<EmergencyRegistration?> FindEmergencyRegistrationAsync(Guid id, CancellationToken ct);
    Task InsertEmergencyRegistrationAsync(EmergencyRegistration registration, CancellationToken ct);
    Task UpdateEmergencyRegistrationAsync(EmergencyRegistration registration, CancellationToken ct);
    Task InsertTriageAssessmentAsync(TriageAssessment assessment, CancellationToken ct);
    Task InsertEmergencyObservationAsync(EmergencyObservation observation, CancellationToken ct);
    Task InsertEmergencyDispositionAsync(EmergencyDisposition disposition, CancellationToken ct);
    Task<OtBooking?> FindOtBookingAsync(Guid id, CancellationToken ct);
    Task InsertOtBookingAsync(OtBooking booking, CancellationToken ct);
    Task UpdateOtBookingAsync(OtBooking booking, CancellationToken ct);
    Task<OtChecklist?> FindOtChecklistAsync(Guid bookingId, CancellationToken ct);
    Task InsertOtChecklistAsync(OtChecklist checklist, CancellationToken ct);
    Task<bool> HasOtConflictAsync(string theatre, DateTimeOffset start, DateTimeOffset end, Guid? excludeId, CancellationToken ct);
    Task<BloodUnit?> FindBloodUnitAsync(Guid id, CancellationToken ct);
    Task<BloodUnit?> FindBloodUnitByNumberAsync(string unitNumber, CancellationToken ct);
    Task InsertBloodUnitAsync(BloodUnit unit, CancellationToken ct);
    Task UpdateBloodUnitAsync(BloodUnit unit, CancellationToken ct);
    Task<BloodScreening?> FindScreeningAsync(Guid unitId, CancellationToken ct);
    Task InsertScreeningAsync(BloodScreening screening, CancellationToken ct);
    Task<Crossmatch?> FindActiveCrossmatchAsync(Guid unitId, Guid patientId, CancellationToken ct);
    Task InsertCrossmatchAsync(Crossmatch crossmatch, CancellationToken ct);
    Task<BloodIssue?> FindBloodIssueAsync(Guid id, CancellationToken ct);
    Task<BloodIssue?> FindOpenBloodIssueAsync(Guid unitId, CancellationToken ct);
    Task InsertBloodIssueAsync(BloodIssue issue, CancellationToken ct);
    Task UpdateBloodIssueAsync(BloodIssue issue, CancellationToken ct);
}
