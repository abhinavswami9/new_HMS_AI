namespace Careflow.Domain;

// Phase 3: radiology/RIS, emergency, operation theatre and blood bank records.
public sealed record RadiologyOrder(Guid Id, string Number, Guid PatientId, Guid? EncounterId, Guid? AdmissionId,
    string Procedure, string Modality, string BodyPart, string Laterality, string Priority, string Status,
    string? Accession, string ClinicalNote, Guid OrderedBy, DateTimeOffset OrderedAt,
    DateTimeOffset? ScheduledAt, DateTimeOffset? PerformedAt, string? StudyUid, long Version = 1);
public sealed record RadiologyReport(Guid Id, Guid OrderId, int Version, string Findings, string Impression,
    bool Critical, string Status, Guid ReportedBy, DateTimeOffset ReportedAt,
    Guid? AuthorizedBy, DateTimeOffset? AuthorizedAt, string? ContentHash);
public sealed record EmergencyRegistration(Guid Id, string Number, Guid? PatientId, string PatientName, string Sex,
    DateOnly? DateOfBirth, string ArrivalMode, string Status, DateTimeOffset ArrivedAt, Guid CreatedBy, long Version = 1);
public sealed record TriageAssessment(Guid Id, Guid RegistrationId, string Category, string Complaint,
    VitalSet Vitals, Guid AssessedBy, DateTimeOffset AssessedAt);
public sealed record EmergencyObservation(Guid Id, Guid RegistrationId, string Note, VitalSet Vitals,
    Guid RecordedBy, DateTimeOffset RecordedAt);
public sealed record EmergencyDisposition(Guid Id, Guid RegistrationId, string Kind, string Summary,
    Guid? AdmissionId, string? ReceivingFacility, Guid DecidedBy, DateTimeOffset DecidedAt);
public sealed record OtBooking(Guid Id, string Number, Guid PatientId, Guid? AdmissionId, string Procedure,
    string Laterality, string Site, Guid SurgeonId, string Anaesthesia, string Priority, string Status,
    string Theatre, DateTimeOffset? ScheduledStart, DateTimeOffset? ScheduledEnd, DateTimeOffset RequestedAt,
    Guid RequestedBy, DateTimeOffset? TimeIn, DateTimeOffset? TimeOut, string OperationNote, long Version = 1);
public sealed record OtChecklist(Guid Id, Guid BookingId, bool Consent, bool AnaesthesiaAssessment,
    bool FastingConfirmed, bool SiteMarked, bool SafetyChecklist, string Notes, Guid RecordedBy, DateTimeOffset RecordedAt);
public sealed record BloodUnit(Guid Id, string UnitNumber, string BloodGroup, string Component, DateOnly ExpiryDate,
    string Status, string DonorName, DateTimeOffset CollectedAt, long Version = 1);
public sealed record BloodScreening(Guid Id, Guid UnitId, bool Hbsag, bool Hiv, bool Hcv, bool Vdrl, string Notes,
    Guid TestedBy, DateTimeOffset TestedAt);
public sealed record Crossmatch(Guid Id, string Number, Guid UnitId, Guid PatientId, string Compatibility,
    string Status, Guid TestedBy, DateTimeOffset TestedAt);
public sealed record BloodIssue(Guid Id, string Number, Guid UnitId, Guid PatientId, Guid? AdmissionId,
    string Status, string Reaction, string ReturnAssessment, Guid IssuedBy, DateTimeOffset IssuedAt,
    DateTimeOffset? TransfusedAt, string Notes, long Version = 1);

/// <summary>ABO/RhD component compatibility. Red-cell products use the red-cell matrix,
/// plasma products the reciprocal plasma matrix. This is a safety floor for the slice,
/// not a substitute for the hospital's transfusion policy and bedside checks.</summary>
public static class BloodRules
{
    public static readonly IReadOnlyList<string> Groups = ["O-", "O+", "A-", "A+", "B-", "B+", "AB-", "AB+"];
    public static readonly IReadOnlyList<string> Components = ["WholeBlood", "PRBC", "FFP", "Platelets"];
    public static bool Compatible(string donor, string recipient, string component)
    {
        if (!Groups.Contains(donor) || !Groups.Contains(recipient) || !Components.Contains(component)) return false;
        var donorAbo = donor[..^1]; var recipientAbo = recipient[..^1];
        var donorRhPositive = donor.EndsWith('+'); var recipientRhPositive = recipient.EndsWith('+');
        if (component is "FFP")
        {
            // Plasma compatibility runs reciprocally and ignores RhD.
            return donorAbo switch
            {
                "AB" => true,
                "A" => recipientAbo is "A" or "O",
                "B" => recipientAbo is "B" or "O",
                "O" => recipientAbo == "O",
                _ => false
            };
        }
        // Red-cell containing products: donor red cells must fit the recipient, and RhD
        // positive red cells must not be given to an RhD negative recipient.
        var aboOk = donorAbo switch
        {
            "O" => true,
            "A" => recipientAbo is "A" or "AB",
            "B" => recipientAbo is "B" or "AB",
            "AB" => recipientAbo == "AB",
            _ => false
        };
        return aboOk && (!donorRhPositive || recipientRhPositive);
    }
}
