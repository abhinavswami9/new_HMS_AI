using Careflow.Application;
using Careflow.Domain;
namespace Careflow.Infrastructure;

public static class SeedIds
{
    public static readonly Guid Hospital = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public static readonly Guid Branch = Guid.Parse("20000000-0000-0000-0000-000000000001");
    public static readonly Guid Admin = Guid.Parse("30000000-0000-0000-0000-000000000001");
    public static readonly Guid Doctor = Guid.Parse("40000000-0000-0000-0000-000000000001");
}
public sealed record LoginAttempt(Guid? UserId, string UserName, bool Success, DateTimeOffset At, string? SourceIp = null);
public sealed record OutboxEntry(Guid Id, string Type, Guid AggregateId, string Payload);
public sealed class DemoData
{
    public List<Patient> Patients { get; set; } = [];
    public List<Doctor> Doctors { get; set; } = [];
    public List<Appointment> Appointments { get; set; } = [];
    public List<Encounter> Encounters { get; set; } = [];
    public List<Invoice> Invoices { get; set; } = [];
    public List<ServiceItem> Services { get; set; } = [];
    public List<Medicine> Medicines { get; set; } = [];
    public List<ModuleSetting> Modules { get; set; } = [];
    public List<AuditEvent> Audit { get; set; } = [];
    public List<UserAccount> Users { get; set; } = [];
    public List<UserSession> Sessions { get; set; } = [];
    public Dictionary<string, long> Sequences { get; set; } = [];
    public List<LoginAttempt> Logins { get; set; } = [];
    public List<OutboxEntry> Outbox { get; set; } = [];
    public string HospitalName { get; set; } = "Nova Care Hospital";
    public string BranchName { get; set; } = "Main branch · New Delhi";
    public Workspace Snapshot(int limit) => new([.. Patients.OrderByDescending(x => x.CreatedAt).Take(limit)], [.. Appointments.OrderByDescending(x => x.StartsAt).Take(limit)],
        [.. Encounters.OrderByDescending(x => x.UpdatedAt).Take(limit)], [.. Invoices.OrderByDescending(x => x.CreatedAt).Take(limit)], Doctors, Services, Medicines, Modules, [.. Audit.OrderByDescending(x => x.OccurredAt).Take(limit)], HospitalName, BranchName);
    public static DemoData Create(IClock clock, IPasswordService passwords)
    {
        var now = clock.UtcNow; var today = now.UtcDateTime.Date; var names = new[] { ("Aarav", "Sharma", 28, "Male"), ("Priya", "Nair", 34, "Female"), ("Rohan", "Mehta", 45, "Male"), ("Ananya", "Gupta", 29, "Female"), ("Vikram", "Singh", 52, "Male"), ("Meera", "Iyer", 39, "Female"), ("Arjun", "Patel", 31, "Male"), ("Kavita", "Reddy", 42, "Female"), ("Suresh", "Kumar", 61, "Male"), ("Neha", "Verma", 26, "Female"), ("Aditya", "Rao", 35, "Male"), ("Ishita", "Shah", 32, "Female") };
        var d = new DemoData();
        d.Patients = names.Select((n, i) => new Patient(Guid.Parse($"50000000-0000-0000-0000-{i + 1:D12}"), $"NCH-{now.Year}-{1241 + i:D6}", n.Item1, n.Item2,
            new DateOnly(now.Year - n.Item3, i % 12 + 1, Math.Min(13 + i, 28)), n.Item4, $"+91 98{76543210 + i}", $"{n.Item1.ToLower()}@example.com",
            new[] { "B+", "O+", "A+", "Unknown", "AB+", "O-" }[i % 6], $"{21 + i} Green Park, New Delhi, 110016", $"Family contact · +91 99{87654321 + i}", now.AddHours(-i * 3))).ToList();
        d.Doctors = [new(SeedIds.Doctor, "Dr. Anjali Sharma", "Cardiology", "AS", "#568f83"), new(Guid.Parse("40000000-0000-0000-0000-000000000002"), "Dr. Rahul Verma", "General Medicine", "RV", "#668eaf"), new(Guid.Parse("40000000-0000-0000-0000-000000000003"), "Dr. Priya Menon", "Orthopaedics", "PM", "#9b85af"), new(Guid.Parse("40000000-0000-0000-0000-000000000004"), "Dr. Arjun Desai", "Pediatrics", "AD", "#c1a064"), new(Guid.Parse("40000000-0000-0000-0000-000000000005"), "Dr. Neha Kapoor", "Dermatology", "NK", "#b58491")];
        d.Services = [new(Guid.Parse("60000000-0000-0000-0000-000000000001"), "General consultation", "General Medicine", 600), new(Guid.Parse("60000000-0000-0000-0000-000000000002"), "Specialist consultation", "Cardiology", 1000), new(Guid.Parse("60000000-0000-0000-0000-000000000003"), "Complete blood count (CBC)", "Laboratory", 350), new(Guid.Parse("60000000-0000-0000-0000-000000000004"), "X-ray · single view", "Radiology", 900), new(Guid.Parse("60000000-0000-0000-0000-000000000005"), "Electrocardiogram (ECG)", "Cardiology", 500), new(Guid.Parse("60000000-0000-0000-0000-000000000006"), "Dressing & wound care", "Procedures", 250), new(Guid.Parse("60000000-0000-0000-0000-000000000007"), "Follow-up consultation", "General Medicine", 400)];
        d.Medicines = [new(Guid.Parse("70000000-0000-0000-0000-000000000001"), "Paracetamol", "Crocin", "500 mg", "Tablet"), new(Guid.Parse("70000000-0000-0000-0000-000000000002"), "Amoxicillin", "Mox", "500 mg", "Capsule"), new(Guid.Parse("70000000-0000-0000-0000-000000000003"), "Cetirizine", "Cetzine", "10 mg", "Tablet"), new(Guid.Parse("70000000-0000-0000-0000-000000000004"), "Metformin", "Glycomet", "500 mg", "Tablet"), new(Guid.Parse("70000000-0000-0000-0000-000000000005"), "Amlodipine", "Amlong", "5 mg", "Tablet"), new(Guid.Parse("70000000-0000-0000-0000-000000000006"), "Pantoprazole", "Pantocid", "40 mg", "Tablet"), new(Guid.Parse("70000000-0000-0000-0000-000000000007"), "Azithromycin", "Azithral", "500 mg", "Tablet"), new(Guid.Parse("70000000-0000-0000-0000-000000000008"), "Ibuprofen", "Brufen", "400 mg", "Tablet")];
        var starts = new[] { today.AddHours(9), today.AddHours(9).AddMinutes(30), today.AddHours(10), today.AddHours(10).AddMinutes(30), today.AddHours(11), today.AddHours(11).AddMinutes(30), today.AddHours(14), today.AddHours(15), today.AddHours(16) };
        var statuses = new[] { "Completed", "In consultation", "Checked in", "Booked", "Checked in", "Booked", "Booked", "Booked", "Booked" };
        d.Appointments = starts.Select((s, i) => new Appointment(Guid.Parse($"80000000-0000-0000-0000-{i + 1:D12}"), d.Patients[i].Id, d.Doctors[i % d.Doctors.Count].Id, new DateTimeOffset(s, TimeSpan.Zero), 20, i == 3 ? "Follow-up" : "In person",
            new[] { "Routine cardiovascular review", "Fever and sore throat for 3 days", "Persistent knee pain", "Follow-up review", "Annual health check", "Skin irritation", "General consultation", "Recurring headache", "Blood pressure review" }[i], statuses[i], statuses[i] is "Checked in" or "In consultation" or "Completed" ? i + 1 : null, now.AddHours(-4))).ToList();
        var draft = new Encounter(Guid.Parse("90000000-0000-0000-0000-000000000001"), d.Patients[1].Id, d.Appointments[1].Id, SeedIds.Doctor, "Draft",
            "Fever and sore throat for three days", "Mild pharyngeal erythema. Chest clear. Hydration adequate.", "Acute pharyngitis", "Supportive care, hydration and rest. Return sooner for breathing difficulty or persistent fever.", "Review in 3 days if symptoms persist",
            new(37.8m, 88, 118, 76, 58, 99), [new("Penicillin", "Reported rash — clinician review required", "Moderate")], [new(d.Medicines[0].Id, "Paracetamol 500 mg", "1 tablet", "Twice daily", 3, "After food; review maximum daily dose")], [new("Laboratory", "Complete blood count", "If fever persists beyond 48 hours")], now.AddMinutes(-20)) { AllergyReviewed = true };
        var historical = new Encounter(Guid.Parse("90000000-0000-0000-0000-000000000002"), d.Patients[0].Id, d.Appointments[0].Id, SeedIds.Doctor, "Draft",
            "Routine cardiovascular review", "Regular rhythm. No pedal edema.", "Essential hypertension — stable", "Continue current treatment and lifestyle measures.", "Review in 4 weeks with home BP log", new(36.7m, 74, 128, 82, 72, 98), [],
            [new(d.Medicines[4].Id, "Amlodipine 5 mg", "1 tablet", "Once daily", 28, "As previously tolerated; monitor blood pressure")], [], now.AddHours(-2)) { AllergyReviewed = true };
        historical = historical with { Status = "Signed", SignedBy = d.Doctors[0].Name, SignedAt = now.AddHours(-1), ContentHash = HospitalService.CanonicalHash(historical), Version = 2 };
        var secondDraft = new Encounter(Guid.Parse("90000000-0000-0000-0000-000000000003"), d.Patients[4].Id, null, d.Doctors[1].Id, "Draft", "Fatigue and increased thirst", "Routine systemic examination documented for review.", "Type 2 diabetes mellitus — review required", "Investigation and lifestyle review", "Review with reports", new(36.9m, 80, 132, 86, 81, 97), [], [], [new("Laboratory", "HbA1c", "Fasting sample preferred")], now.AddMinutes(-40));
        d.Encounters = [draft, secondDraft, historical];
        d.Invoices = Enumerable.Range(0, 7).Select(i =>
        {
            var lines = new[] { new InvoiceLine(d.Services[i % d.Services.Count].Id, d.Services[i % d.Services.Count].Name, 1, d.Services[i % d.Services.Count].Price) };
            var payments = i is 0 or 3 or 5 ? new[] { new Payment(Guid.Parse($"a0000000-0000-0000-0000-{i + 1:D12}"), lines.Sum(l => l.Quantity * l.UnitPrice), i == 0 ? "UPI" : "Card", $"SYNTH-PAY-{i + 1:D5}", now.AddHours(-i), SeedIds.Admin) } : Array.Empty<Payment>();
            return new Invoice(Guid.Parse($"b0000000-0000-0000-0000-{i + 1:D12}"), $"INV-{now.Year}-{i + 1:D4}", d.Patients[i].Id, null, i == 6 ? "Draft" : payments.Length > 0 ? "Paid" : "Issued", lines, 0, 0, payments, now.AddHours(-i - 1), now.AddHours(-i - 1));
        }).ToList();
        d.Modules = [new("patients", "Patient registration & UHID", true, 1, "A unique patient identity, demographics, duplicate review and a longitudinal chart."), new("appointments", "Appointments & queues", true, 1, "Practitioner scheduling, booking, check-in and live patient-flow management."), new("consultations", "OPD & doctor EMR", true, 1, "Clinical notes, vitals, allergies, treatment plans and signed medical records."), new("billing", "Billing & payments", true, 1, "Service-based invoices, controlled issue and traceable payment collection."), new("ipd", "Inpatient management", false, 2, "Admission, discharge, transfer, care teams and discharge summaries."), new("nursing", "Nursing & bed management", false, 2, "Visual bed board, nursing assessments, eMAR and shift handovers."), new("pharmacy", "Pharmacy management", false, 2, "Prescription dispensing, batch traceability, stock and expiry control."), new("laboratory", "Laboratory · LIS", false, 2, "Specimen tracking, result validation, authorization and NABL-oriented quality."), new("radiology", "Radiology · RIS", false, 3, "Modality scheduling, worklists, structured reports and approval."), new("pacs", "PACS & DICOM", false, 3, "Standards-based imaging archive integration and secure viewer access."), new("emergency", "Emergency & casualty", false, 3, "Rapid registration, triage, observation and emergency disposition."), new("ot", "Operation theatre", false, 3, "Surgical scheduling, safety checklists, implants and recovery."), new("blood", "Blood bank", false, 3, "Donor-to-recipient traceability, testing, crossmatch and inventory."), new("insurance", "Insurance & TPA", false, 4, "Coverage, pre-authorization, claims and settlement reconciliation."), new("incentives", "Doctor incentives", false, 4, "Configurable incentive rules, calculation, approval and payment reporting."), new("telemedicine", "Telemedicine", false, 4, "Consent-aware video consultations, waiting rooms and digital care records."), new("inventory", "Inventory & procurement", false, 4, "Central and department stores, purchasing, GRN and asset management."), new("cssd", "CSSD", false, 4, "Instrument tracking, sterilization cycles, release and recall traceability."), new("quality", "Quality & infection control", false, 5, "Configurable NABH/NABL-oriented audits, incidents, CAPA and indicators."), new("portal", "Patient portal", false, 5, "Verified access to appointments, records, reports, bills and consent."), new("reports", "Advanced reporting", false, 5, "Governed dashboards, a configurable report builder and audited exports.")];
        d.Users = [new(SeedIds.Admin, "admin", "Anjali Sharma", passwords.Hash("Preview!2026"), "Hospital Admin", SeedIds.Hospital, SeedIds.Branch, true), new(SeedIds.Doctor, "doctor", d.Doctors[0].Name, passwords.Hash("Preview!2026"), "Doctor", SeedIds.Hospital, SeedIds.Branch, true), .. new[] { ("reception", "Riya Kapoor", "Receptionist"), ("billing", "Amit Joshi", "Billing Staff"), ("nurse", "Nisha Thomas", "Nurse") }.Select((x, i) => new UserAccount(Guid.Parse($"30000000-0000-0000-0000-{i + 2:D12}"), x.Item1, x.Item2, passwords.Hash("Preview!2026"), x.Item3, SeedIds.Hospital, SeedIds.Branch, true))];
        d.Sequences = new() { ["UHID:" + SeedIds.Hospital] = 1253, [$"INVOICE:{SeedIds.Branch}:{now.Year}"] = 7 };
        d.Audit = [new(Guid.NewGuid(), SeedIds.Admin, "Anjali Sharma", "patient.registered", "Patient", d.Patients[0].Id, "Synthetic preview seed", now.AddMinutes(8)), new(Guid.NewGuid(), SeedIds.Admin, "Anjali Sharma", "appointment.booked", "Appointment", d.Appointments[2].Id, "Synthetic preview seed", now.AddMinutes(12)), new(Guid.NewGuid(), SeedIds.Doctor, d.Doctors[0].Name, "encounter.signed", "Encounter", historical.Id, "Synthetic preview seed", now.AddMinutes(18)), new(Guid.NewGuid(), SeedIds.Admin, "Amit Joshi", "invoice.payment.recorded", "Invoice", d.Invoices[0].Id, "Synthetic preview seed", now.AddMinutes(24))];
        return d;
    }
}
