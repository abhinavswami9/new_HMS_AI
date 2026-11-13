namespace Careflow.Domain;

public static class Permissions
{
    public const string PatientRead = "patient.read";
    public const string PatientWrite = "patient.write";
    public const string ScheduleRead = "schedule.read";
    public const string ScheduleWrite = "schedule.write";
    public const string ClinicalRead = "clinical.read";
    public const string ClinicalWrite = "clinical.write";
    public const string ClinicalSign = "clinical.sign";
    public const string BillingRead = "billing.read";
    public const string BillingWrite = "billing.write";
    public const string BillingCollect = "billing.collect";
    public const string Admin = "security.admin";
    public const string AuditRead = "audit.read";
    public const string IpdRead = "ipd.read"; public const string IpdWrite = "ipd.write"; public const string IpdDischarge = "ipd.discharge"; public const string IpdBed = "ipd.bed";
    public const string NursingRead = "nursing.read"; public const string NursingWrite = "nursing.write"; public const string NursingOrder = "nursing.order"; public const string NursingAdminister = "nursing.administer"; public const string NursingHandover = "nursing.handover";
    public const string PharmacyRead = "pharmacy.read"; public const string PharmacyWrite = "pharmacy.write"; public const string PharmacyDispense = "pharmacy.dispense";
    public const string LabRead = "lab.read"; public const string LabOrder = "lab.order"; public const string LabCollect = "lab.collect"; public const string LabProcess = "lab.process"; public const string LabResult = "lab.result"; public const string LabValidate = "lab.validate"; public const string LabAuthorize = "lab.authorize"; public const string LabAcknowledge = "lab.acknowledge";
    public static readonly IReadOnlyDictionary<string, string[]> Roles = new Dictionary<string, string[]>
    {
        ["Hospital Admin"] = [PatientRead, PatientWrite, ScheduleRead, ScheduleWrite, ClinicalRead, BillingRead, BillingWrite, BillingCollect, Admin, AuditRead, IpdRead, IpdWrite, IpdBed, NursingRead, PharmacyRead, LabRead, LabAcknowledge],
        ["Doctor"] = [PatientRead, ScheduleRead, ScheduleWrite, ClinicalRead, ClinicalWrite, ClinicalSign, IpdRead, IpdWrite, IpdDischarge, NursingRead, NursingWrite, NursingOrder, LabRead, LabOrder, LabAcknowledge],
        ["Receptionist"] = [PatientRead, PatientWrite, ScheduleRead, ScheduleWrite, IpdRead, IpdWrite],
        ["Billing Staff"] = [PatientRead, ScheduleRead, BillingRead, BillingWrite, BillingCollect],
        ["Nurse"] = [PatientRead, ScheduleRead, ClinicalRead, IpdRead, IpdBed, NursingRead, NursingWrite, NursingAdminister, NursingHandover, LabRead, LabAcknowledge],
        ["Pharmacist"] = [PatientRead, ClinicalRead, PharmacyRead, PharmacyWrite, PharmacyDispense],
        ["Lab Technician"] = [PatientRead, LabRead, LabCollect, LabProcess, LabResult, LabValidate],
        ["Pathologist"] = [PatientRead, ClinicalRead, LabRead, LabAuthorize, LabAcknowledge]
    };
    public static IReadOnlyList<string> For(string role) => Roles.TryGetValue(role, out var p) ? p : [];
}

public sealed record Principal(Guid Id, string Name, string Role, Guid HospitalId, Guid BranchId)
{
    public IReadOnlyList<string> Permissions { get; init; } = Domain.Permissions.For(Role);
    public bool Has(string permission) => Permissions.Contains(permission);
}
public sealed record Doctor(Guid Id, string Name, string Department, string Initials, string Color);
public sealed record Patient(Guid Id, string Uhid, string FirstName, string LastName, DateOnly DateOfBirth,
    string Sex, string Phone, string Email, string BloodGroup, string Address, string EmergencyContact,
    DateTimeOffset CreatedAt, bool Active = true, long Version = 1)
{
    public string FullName => $"{FirstName} {LastName}";
}
public sealed record ServiceItem(Guid Id, string Name, string Department, decimal Price);
public sealed record Medicine(Guid Id, string GenericName, string BrandName, string Strength, string Form);
public sealed record VitalSet(decimal? TemperatureC, int? Pulse, int? Systolic, int? Diastolic, decimal? WeightKg, int? Spo2);
public sealed record Allergy(string Substance, string Reaction, string Severity);
public sealed record PrescriptionItem(Guid MedicineId, string Name, string Dose, string Frequency, int DurationDays, string Instructions);
public sealed record ClinicalOrder(string Kind, string Name, string Instructions);
public sealed record Encounter(Guid Id, Guid PatientId, Guid? AppointmentId, Guid DoctorId, string Status,
    string ChiefComplaint, string Examination, string Diagnosis, string Plan, string FollowUp,
    VitalSet Vitals, IReadOnlyList<Allergy> Allergies, IReadOnlyList<PrescriptionItem> Prescription,
    IReadOnlyList<ClinicalOrder> Orders, DateTimeOffset UpdatedAt, string? SignedBy = null,
    DateTimeOffset? SignedAt = null, string? ContentHash = null, long Version = 1)
{
    public bool AllergyReviewed { get; init; }
}
public sealed record Appointment(Guid Id, Guid PatientId, Guid DoctorId, DateTimeOffset StartsAt,
    int DurationMinutes, string Mode, string Reason, string Status, int? Token, DateTimeOffset CreatedAt, long Version = 1);
public sealed record InvoiceLine(Guid ServiceId, string Name, int Quantity, decimal UnitPrice);
public sealed record Payment(Guid Id, decimal Amount, string Method, string Reference, DateTimeOffset ReceivedAt, Guid ReceivedBy);
public sealed record Invoice(Guid Id, string Number, Guid PatientId, Guid? EncounterId, string Status,
    IReadOnlyList<InvoiceLine> Lines, decimal Discount, decimal TaxRate, IReadOnlyList<Payment> Payments,
    DateTimeOffset CreatedAt, DateTimeOffset? IssuedAt = null, long Version = 1)
{
    public decimal Subtotal => Lines.Sum(x => x.Quantity * x.UnitPrice);
    public decimal Tax => decimal.Round((Subtotal - Discount) * TaxRate, 2, MidpointRounding.AwayFromZero);
    public decimal Total => Subtotal - Discount + Tax;
    public decimal Paid => Payments.Sum(x => x.Amount);
    public decimal Balance => Total - Paid;
}
public sealed record ModuleSetting(string Key, string Name, bool Enabled, int Phase, string Description);
public sealed record AuditEvent(Guid Id, Guid ActorId, string ActorName, string Action, string Entity,
    Guid EntityId, string Reason, DateTimeOffset OccurredAt, string? Changes = null);
public sealed record Chart(Patient Patient, IReadOnlyList<Encounter> Encounters, IReadOnlyList<Appointment> Appointments, IReadOnlyList<Invoice> Invoices, bool HistoryTruncated = false)
{
    public CareChart Care { get; init; } = CareChart.Empty;
}
public sealed record Workspace(IReadOnlyList<Patient> Patients, IReadOnlyList<Appointment> Appointments,
    IReadOnlyList<Encounter> Encounters, IReadOnlyList<Invoice> Invoices, IReadOnlyList<Doctor> Doctors,
    IReadOnlyList<ServiceItem> Services, IReadOnlyList<Medicine> Medicines, IReadOnlyList<ModuleSetting> Modules,
    IReadOnlyList<AuditEvent> Audit, string HospitalName, string BranchName)
{
    public CareWorkspace Care { get; init; } = CareWorkspace.Empty;
}

public class DomainException(string code, string message, int statusCode = 422) : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
}
public static class ClinicalRules
{
    public static void ValidatePatient(string firstName, string lastName, DateOnly dob, string phone, string sex, DateOnly today)
    {
        if (string.IsNullOrWhiteSpace(firstName) || firstName.Length > 80 || string.IsNullOrWhiteSpace(lastName) || lastName.Length > 80)
            throw new DomainException("patient.name", "First and last names are required (maximum 80 characters each).");
        if (dob > today || dob < today.AddYears(-120)) throw new DomainException("patient.dob", "Enter a valid date of birth, not in the future.");
        if (!new[] { "Male", "Female", "Other", "Undisclosed" }.Contains(sex)) throw new DomainException("patient.sex", "Select a supported sex/gender value.");
        if (!string.IsNullOrEmpty(phone) && (phone.Length < 7 || phone.Length > 20 || !phone.All(c => char.IsDigit(c) || "+ -()".Contains(c))))
            throw new DomainException("patient.phone", "Enter a valid contact number.");
    }
    public static void ValidateVitals(VitalSet v)
    {
        var checks = new (string Name, decimal? Value, decimal Min, decimal Max)[]
        { ("Temperature", v.TemperatureC, 25, 45), ("Pulse", v.Pulse, 10, 300),
          ("Systolic pressure", v.Systolic, 30, 350), ("Diastolic pressure", v.Diastolic, 10, 250),
          ("Weight", v.WeightKg, 0.1m, 500), ("Oxygen saturation", v.Spo2, 10, 100) };
        foreach (var c in checks) if (c.Value.HasValue && (c.Value < c.Min || c.Value > c.Max))
            throw new DomainException("vitals.range", $"{c.Name} is outside the accepted entry range ({c.Min}–{c.Max}).");
        if (v.Systolic.HasValue && v.Diastolic.HasValue && v.Diastolic >= v.Systolic)
            throw new DomainException("vitals.pressure", "Systolic pressure must exceed diastolic pressure.");
    }
    public static void ValidatePrescription(IEnumerable<PrescriptionItem> items, IEnumerable<Allergy> allergies, IReadOnlySet<Guid> formulary)
    {
        foreach (var item in items)
        {
            if (!formulary.Contains(item.MedicineId)) throw new DomainException("medicine.unknown", "Select a medicine from the approved formulary.");
            if (string.IsNullOrWhiteSpace(item.Dose) || item.Dose.Length > 80 || string.IsNullOrWhiteSpace(item.Frequency) || item.Frequency.Length > 80 || item.DurationDays is < 1 or > 365)
                throw new DomainException("prescription.incomplete", "Each medicine needs a dose, frequency and duration of 1–365 days.");
            if (item.Instructions.Length > 500) throw new DomainException("prescription.instructions", "Instructions are limited to 500 characters.");
            var generic = item.Name.Split(' ')[0];
            if (allergies.Any(a => a.Substance.Contains(generic, StringComparison.OrdinalIgnoreCase) || generic.Contains(a.Substance, StringComparison.OrdinalIgnoreCase)))
                throw new DomainException("prescription.allergy", $"Potential allergy: {item.Name}. Review the recorded allergy before prescribing.");
        }
    }
    public static void ValidatePayment(decimal amount, decimal balance)
    {
        if (amount <= 0 || decimal.Round(amount, 2) != amount) throw new DomainException("payment.amount", "Payment must be positive with at most two decimal places.");
        if (amount > balance) throw new DomainException("payment.overpayment", "Payment cannot exceed the outstanding balance. Use a separately approved advance workflow.");
    }
}
