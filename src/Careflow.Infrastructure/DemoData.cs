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
    public List<Bed> Beds { get; set; } = [];
    public List<Admission> Admissions { get; set; } = [];
    public List<BedEvent> BedEvents { get; set; } = [];
    public List<NursingNote> NursingNotes { get; set; } = [];
    public List<MedicationOrder> MedicationOrders { get; set; } = [];
    public List<MedicationAdministration> Administrations { get; set; } = [];
    public List<ShiftHandover> Handovers { get; set; } = [];
    public List<StockBatch> StockBatches { get; set; } = [];
    public List<StockMovement> StockMovements { get; set; } = [];
    public List<Dispense> Dispenses { get; set; } = [];
    public List<LabTest> LabTests { get; set; } = [];
    public List<LabOrder> LabOrders { get; set; } = [];
    public List<LabResult> LabResults { get; set; } = [];
    public List<CriticalAlert> CriticalAlerts { get; set; } = [];
    public List<RadiologyOrder> RadiologyOrders { get; set; } = [];
    public List<RadiologyReport> RadiologyReports { get; set; } = [];
    public List<EmergencyRegistration> EmergencyRegistrations { get; set; } = [];
    public List<TriageAssessment> TriageAssessments { get; set; } = [];
    public List<EmergencyObservation> EmergencyObservations { get; set; } = [];
    public List<EmergencyDisposition> EmergencyDispositions { get; set; } = [];
    public List<OtBooking> OtBookings { get; set; } = [];
    public List<OtChecklist> OtChecklists { get; set; } = [];
    public List<BloodUnit> BloodUnits { get; set; } = [];
    public List<BloodScreening> BloodScreenings { get; set; } = [];
    public List<Crossmatch> Crossmatches { get; set; } = [];
    public List<BloodIssue> BloodIssues { get; set; } = [];
    public List<Payer> Payers { get; set; } = [];
    public List<Coverage> Coverages { get; set; } = [];
    public List<Preauthorization> Preauthorizations { get; set; } = [];
    public List<Claim> Claims { get; set; } = [];
    public List<ClaimLine> ClaimLines { get; set; } = [];
    public List<ClaimQuery> ClaimQueries { get; set; } = [];
    public List<Settlement> Settlements { get; set; } = [];
    public List<InsurancePlan> InsurancePlans { get; set; } = [];
    public List<IncentiveRule> IncentiveRules { get; set; } = [];
    public List<IncentiveBatch> IncentiveBatches { get; set; } = [];
    public List<IncentiveLine> IncentiveLines { get; set; } = [];
    public List<IncentivePayment> IncentivePayments { get; set; } = [];
    public List<TeleSession> TeleSessions { get; set; } = [];
    public List<TeleConsentEvent> TeleConsentEvents { get; set; } = [];
    public List<TeleWaitingEvent> TeleWaitingEvents { get; set; } = [];
    public List<TeleUpload> TeleUploads { get; set; } = [];
    public List<InventoryStore> InventoryStores { get; set; } = [];
    public List<InventoryItem> InventoryItems { get; set; } = [];
    public List<InventoryBatch> InventoryBatches { get; set; } = [];
    public List<InventoryMovement> InventoryMovements { get; set; } = [];
    public List<Vendor> Vendors { get; set; } = [];
    public List<InventoryIndent> InventoryIndents { get; set; } = [];
    public List<PurchaseOrder> PurchaseOrders { get; set; } = [];
    public List<GoodsReceipt> GoodsReceipts { get; set; } = [];
    public List<InventoryTransfer> InventoryTransfers { get; set; } = [];
    public List<InventoryConsumption> InventoryConsumptions { get; set; } = [];
    public List<Asset> Assets { get; set; } = [];
    public List<InstrumentSet> InstrumentSets { get; set; } = [];
    public List<InstrumentItem> InstrumentItems { get; set; } = [];
    public List<SterilizerCycle> SterilizerCycles { get; set; } = [];
    public List<CycleLoad> CycleLoads { get; set; } = [];
    public List<CycleIndicator> CycleIndicators { get; set; } = [];
    public List<CssdIssue> CssdIssues { get; set; } = [];
    public List<Infection> Infections { get; set; } = [];
    public List<Incident> Incidents { get; set; } = [];
    public List<RiskAssessment> Risks { get; set; } = [];
    public List<Capa> Capas { get; set; } = [];
    public List<ClinicalAudit> Audits { get; set; } = [];
    public List<QualityIndicator> QualityIndicators { get; set; } = [];
    public List<IndicatorResult> IndicatorResults { get; set; } = [];
    public List<QualityDocument> QualityDocuments { get; set; } = [];
    public List<DocumentVersion> DocumentVersions { get; set; } = [];
    public List<SopAcknowledgment> SopAcknowledgments { get; set; } = [];
    public List<PortalAccount> PortalAccounts { get; set; } = [];
    public List<PatientLink> PatientLinks { get; set; } = [];
    public List<ProxyGrant> ProxyGrants { get; set; } = [];
    public List<DeviceSession> DeviceSessions { get; set; } = [];
    public List<PortalPreference> PortalPreferences { get; set; } = [];
    public List<ReportDefinition> ReportDefinitions { get; set; } = [];
    public List<ReportVersion> ReportVersions { get; set; } = [];
    public List<ReportExecution> ReportExecutions { get; set; } = [];
    public List<ReportExport> ReportExports { get; set; } = [];
    public List<ReportSubscription> ReportSubscriptions { get; set; } = [];
    public List<CssdRecall> CssdRecalls { get; set; } = [];
    public Dictionary<string, string> Policies { get; set; } = [];
    public string HospitalName { get; set; } = "Nova Care Hospital";
    public string BranchName { get; set; } = "Main branch · New Delhi";
    public Workspace Snapshot(int limit) => new([.. Patients.OrderByDescending(x => x.CreatedAt).Take(limit)], [.. Appointments.OrderByDescending(x => x.StartsAt).Take(limit)],
        [.. Encounters.OrderByDescending(x => x.UpdatedAt).Take(limit)], [.. Invoices.OrderByDescending(x => x.CreatedAt).Take(limit)], Doctors, Services, Medicines, Modules, [.. Audit.OrderByDescending(x => x.OccurredAt).Take(limit)], HospitalName, BranchName);
    public static DemoData Create(IClock clock, IPasswordService passwords)
    {
        var now = clock.UtcNow; var today = now.UtcDateTime.Date; var names = new[] { ("Dolly", "Chauhan", 28, "Female"), ("Ramesh", "Gupta", 34, "Male"), ("Rohan", "Mehta", 45, "Male"), ("Ananya", "Gupta", 29, "Female"), ("Vikram", "Singh", 52, "Male"), ("Meera", "Iyer", 39, "Female"), ("Arjun", "Patel", 31, "Male"), ("Kavita", "Reddy", 42, "Female"), ("Suresh", "Kumar", 61, "Male"), ("Neha", "Verma", 26, "Female"), ("Aditya", "Rao", 35, "Male"), ("Ishita", "Shah", 32, "Female") };
        var d = new DemoData();
        d.Patients = names.Select((n, i) => new Patient(Guid.Parse($"50000000-0000-0000-0000-{i + 1:D12}"), $"NCH-{now.Year}-{1241 + i:D6}", n.Item1, n.Item2,
            new DateOnly(now.Year - n.Item3, i % 12 + 1, Math.Min(13 + i, 28)), n.Item4, $"+91 98{76543210 + i}", $"{n.Item1.ToLower()}@example.com",
            new[] { "B+", "O+", "A+", "Unknown", "AB+", "O-" }[i % 6], $"{21 + i} Green Park, New Delhi, 110016", $"Family contact · +91 99{87654321 + i}", now.AddHours(-i * 3))).ToList();
        d.Patients = d.Patients.Select((p, i) => p with
        {
            Title = p.Sex == "Female" ? (i % 2 == 0 ? "Ms" : "Mrs") : "Mr",
            MiddleName = i % 3 == 0 ? "Kumar" : i % 3 == 1 ? "Raj" : "",
            MaritalStatus = i % 3 == 0 ? "Married" : i % 3 == 1 ? "Single" : "Married",
            FatherName = $"Mr. {names[i % names.Length].Item2} Senior",
            MotherName = $"Mrs. {names[(i + 4) % names.Length].Item2}",
            SpouseName = i % 3 == 0 ? $"Mrs. {names[(i + 2) % names.Length].Item2}" : "",
            GuardianName = i == 9 ? "Rajiv Verma" : "",
            GuardianRelation = i == 9 ? "Father" : "",
            Religion = new[] { "Hindu", "Muslim", "Sikh", "Christian" }[i % 4],
            Occupation = new[] { "Service", "Business", "Homemaker", "Retired", "Self-employed" }[i % 5],
            Education = new[] { "Graduate", "Post-graduate", "School" }[i % 3],
            Language = i % 4 == 3 ? "English" : "Hindi",
            Nationality = "Indian",
            AadhaarNumber = $"78{i:D3}4567{i:D3}",
            AbhaNumber = i < 5 ? $"{i + 1:D2}3456789012{i:D2}" : "",
            AbhaAddress = i < 5 ? $"{p.FirstName.ToLower()}.{p.LastName.ToLower()}@abdm" : "",
            City = "New Delhi",
            State = "Delhi",
            Pincode = $"1100{i % 10}{(i + 2) % 10}6",
            Country = "India"
        }).ToList();
        d.Doctors = [new(SeedIds.Doctor, "Dr. Anjali Sharma", "Cardiology", "AS", "#568f83"), new(Guid.Parse("40000000-0000-0000-0000-000000000002"), "Dr. Rahul Verma", "General Medicine", "RV", "#668eaf"), new(Guid.Parse("40000000-0000-0000-0000-000000000003"), "Dr. Priya Menon", "Orthopaedics", "PM", "#9b85af"), new(Guid.Parse("40000000-0000-0000-0000-000000000004"), "Dr. Arjun Desai", "Pediatrics", "AD", "#c1a064"), new(Guid.Parse("40000000-0000-0000-0000-000000000005"), "Dr. Neha Kapoor", "Dermatology", "NK", "#b58491")];
        d.Services = [new(Guid.Parse("60000000-0000-0000-0000-000000000001"), "General consultation", "General Medicine", 600), new(Guid.Parse("60000000-0000-0000-0000-000000000002"), "Specialist consultation", "Cardiology", 1000), new(Guid.Parse("60000000-0000-0000-0000-000000000003"), "Complete blood count (CBC)", "Laboratory", 350), new(Guid.Parse("60000000-0000-0000-0000-000000000004"), "X-ray · single view", "Radiology", 900), new(Guid.Parse("60000000-0000-0000-0000-000000000005"), "Electrocardiogram (ECG)", "Cardiology", 500), new(Guid.Parse("60000000-0000-0000-0000-000000000006"), "Dressing & wound care", "Procedures", 250), new(Guid.Parse("60000000-0000-0000-0000-000000000007"), "Follow-up consultation", "General Medicine", 400)];
        d.Medicines = [new(Guid.Parse("70000000-0000-0000-0000-000000000001"), "Paracetamol", "Crocin", "500 mg", "Tablet"), new(Guid.Parse("70000000-0000-0000-0000-000000000002"), "Amoxicillin", "Mox", "500 mg", "Capsule"), new(Guid.Parse("70000000-0000-0000-0000-000000000003"), "Cetirizine", "Cetzine", "10 mg", "Tablet"), new(Guid.Parse("70000000-0000-0000-0000-000000000004"), "Metformin", "Glycomet", "500 mg", "Tablet"), new(Guid.Parse("70000000-0000-0000-0000-000000000005"), "Amlodipine", "Amlong", "5 mg", "Tablet"), new(Guid.Parse("70000000-0000-0000-0000-000000000006"), "Pantoprazole", "Pantocid", "40 mg", "Tablet"), new(Guid.Parse("70000000-0000-0000-0000-000000000007"), "Azithromycin", "Azithral", "500 mg", "Tablet"), new(Guid.Parse("70000000-0000-0000-0000-000000000008"), "Ibuprofen", "Brufen", "400 mg", "Tablet")];
        var starts = new[] { today.AddHours(9), today.AddHours(9).AddMinutes(30), today.AddHours(10), today.AddHours(10).AddMinutes(30), today.AddHours(11), today.AddHours(11).AddMinutes(30), today.AddHours(14), today.AddHours(15), today.AddHours(16) };
        var statuses = new[] { "Completed", "In consultation", "Checked in", "Booked", "Checked in", "Booked", "Booked", "Booked", "Booked" };
        d.Appointments = starts.Select((s, i) => new Appointment(Guid.Parse($"80000000-0000-0000-0000-{i + 1:D12}"), d.Patients[i].Id, d.Doctors[i % d.Doctors.Count].Id, new DateTimeOffset(s, TimeSpan.Zero), 20, i == 3 ? "Follow-up" : "In person",
            new[] { "Routine cardiovascular review", "Fever and sore throat for 3 days", "Persistent knee pain", "Follow-up review", "Annual health check", "Skin irritation", "General consultation", "Recurring headache", "Blood pressure review" }[i], statuses[i], statuses[i] is "Checked in" or "In consultation" or "Completed" ? i + 1 : null, now.AddHours(-4))).ToList();
        var draft = new Encounter(Guid.Parse("90000000-0000-0000-0000-000000000001"), d.Patients[1].Id, d.Appointments[1].Id, SeedIds.Doctor, "Draft",
            "Fever and sore throat for three days", "Mild pharyngeal erythema. Chest clear. Hydration adequate.", "Acute pharyngitis", "Supportive care, hydration and rest. Return sooner for breathing difficulty or persistent fever.", "Review in 3 days if symptoms persist",
            new(37.8m, 88, 118, 76, 58, 99), [new("Penicillin", "Reported rash — clinician review required", "Moderate")], [new(d.Medicines[0].Id, "Paracetamol 500 mg", "1 tablet", "Twice daily", 3, "After food; review maximum daily dose")], [new("Laboratory", "Complete blood count", "If fever persists beyond 48 hours")], now.AddMinutes(-20))
        { AllergyReviewed = true };
        var historical = new Encounter(Guid.Parse("90000000-0000-0000-0000-000000000002"), d.Patients[0].Id, d.Appointments[0].Id, SeedIds.Doctor, "Draft",
            "Routine cardiovascular review", "Regular rhythm. No pedal edema.", "Essential hypertension — stable", "Continue current treatment and lifestyle measures.", "Review in 4 weeks with home BP log", new(36.7m, 74, 128, 82, 72, 98), [],
            [new(d.Medicines[4].Id, "Amlodipine 5 mg", "1 tablet", "Once daily", 28, "As previously tolerated; monitor blood pressure")], [], now.AddDays(-3))
        { AllergyReviewed = true };
        historical = historical with { Status = "Signed", SignedBy = d.Doctors[0].Name, SignedAt = now.AddDays(-3).AddHours(1), ContentHash = HospitalService.CanonicalHash(historical), Version = 2 };
        var secondDraft = new Encounter(Guid.Parse("90000000-0000-0000-0000-000000000003"), d.Patients[4].Id, null, d.Doctors[1].Id, "Draft", "Fatigue and increased thirst", "Routine systemic examination documented for review.", "Type 2 diabetes mellitus — review required", "Investigation and lifestyle review", "Review with reports", new(36.9m, 80, 132, 86, 81, 97), [], [], [new("Laboratory", "HbA1c", "Fasting sample preferred")], now.AddMinutes(-40));
        d.Encounters = [draft, secondDraft, historical];
        d.Invoices = Enumerable.Range(0, 7).Select(i =>
        {
            var lines = new[] { new InvoiceLine(d.Services[i % d.Services.Count].Id, d.Services[i % d.Services.Count].Name, 1, d.Services[i % d.Services.Count].Price) };
            var payments = i is 0 or 3 or 5 ? new[] { new Payment(Guid.Parse($"a0000000-0000-0000-0000-{i + 1:D12}"), lines.Sum(l => l.Quantity * l.UnitPrice), i == 0 ? "UPI" : "Card", $"SYNTH-PAY-{i + 1:D5}", now.AddDays(-3).AddHours(-i), SeedIds.Admin) } : Array.Empty<Payment>();
            return new Invoice(Guid.Parse($"b0000000-0000-0000-0000-{i + 1:D12}"), $"INV-{now.Year}-{i + 1:D4}", d.Patients[i].Id, null, i == 6 ? "Draft" : payments.Length > 0 ? "Paid" : "Issued", lines, 0, 0, payments, now.AddHours(-i - 1), now.AddHours(-i - 1));
        }).ToList();
        d.Modules = [new("patients", "Patient registration & UHID", true, 1, "A unique patient identity, demographics, duplicate review and a longitudinal chart."), new("appointments", "Appointments & queues", true, 1, "Practitioner scheduling, booking, check-in and live patient-flow management."), new("consultations", "OPD & doctor EMR", true, 1, "Clinical notes, vitals, allergies, treatment plans and signed medical records."), new("billing", "Billing & payments", true, 1, "Service-based invoices, controlled issue and traceable payment collection."), new("ipd", "Inpatient management", true, 2, "Admission, discharge, transfer, care teams and discharge summaries."), new("nursing", "Nursing & bed management", true, 2, "Visual bed board, nursing assessments, eMAR and shift handovers."), new("pharmacy", "Pharmacy management", true, 2, "Prescription dispensing, batch traceability, stock and expiry control."), new("laboratory", "Laboratory · LIS", true, 2, "Specimen tracking, result validation, authorization and NABL-oriented quality."), new("radiology", "Radiology · RIS", true, 3, "Modality scheduling, worklists, structured reports and approval."), new("pacs", "PACS & DICOM", false, 3, "Standards-based imaging archive integration and secure viewer access."), new("emergency", "Emergency & casualty", true, 3, "Rapid registration, triage, observation and emergency disposition."), new("ot", "Operation theatre", true, 3, "Surgical scheduling, safety checklists, implants and recovery."), new("blood", "Blood bank", true, 3, "Donor-to-recipient traceability, testing, crossmatch and inventory."), new("insurance", "Insurance & TPA", true, 4, "Coverage, pre-authorization, claims and settlement reconciliation."), new("incentives", "Doctor incentives", true, 4, "Configurable incentive rules, calculation, approval and payment reporting."), new("telemedicine", "Telemedicine", true, 4, "Consent-aware video consultations, waiting rooms and digital care records."), new("inventory", "Inventory & procurement", true, 4, "Central and department stores, purchasing, GRN and asset management."), new("cssd", "CSSD", true, 4, "Instrument tracking, sterilization cycles, release and recall traceability."), new("quality", "Quality & infection control", true, 5, "Configurable NABH/NABL-oriented audits, incidents, CAPA and indicators."), new("portal", "Patient portal", true, 5, "Verified access to appointments, records, reports, bills and consent."), new("reports", "Advanced reporting", true, 5, "Governed dashboards, a configurable report builder and audited exports.")];
        d.Users = [new(SeedIds.Admin, "admin", "Anjali Sharma", passwords.Hash("Preview!2026"), "Hospital Admin", SeedIds.Hospital, SeedIds.Branch, true), new(SeedIds.Doctor, "doctor", d.Doctors[0].Name, passwords.Hash("Preview!2026"), "Doctor", SeedIds.Hospital, SeedIds.Branch, true), .. new[] { ("reception", "Riya Kapoor", "Receptionist"), ("billing", "Amit Joshi", "Billing Staff"), ("nurse", "Nisha Thomas", "Nurse"), ("pharmacist", "Kiran Rao", "Pharmacist"), ("labtech", "Sana Iqbal", "Lab Technician"), ("pathologist", "Dr. Vivek Malhotra", "Pathologist"), ("labtech2", "Rohit Sen", "Lab Technician"), ("nurse2", "Amrita Nair", "Nurse"), ("radiologist", "Dr. Kabir Menon", "Radiologist"), ("radiologist2", "Dr. Arvind Nair", "Radiologist"), ("bloodtech", "Farah Khan", "Blood Bank Technician"), ("insurance", "Ishaan Malhotra", "Insurance Coordinator"), ("account", "Tara Sethi", "Accountant"), ("store", "Manoj Pillai", "Store Manager"), ("cssdtech", "Ritu Bansal", "CSSD Technician"), ("quality", "Vinay Raheja", "Quality Manager"), ("icnurse", "Sneha Iyer", "Infection Control Nurse"), ("depthead", "Dr. Suresh Menon", "Department Head"), ("patient", "Dolly Chauhan", "Patient"), ("patient2", "Ramesh Gupta", "Patient") }.Select((x, i) => new UserAccount(Guid.Parse($"30000000-0000-0000-0000-{i + 2:D12}"), x.Item1, x.Item2, passwords.Hash("Preview!2026"), x.Item3, SeedIds.Hospital, SeedIds.Branch, true))];
        d.Sequences = new() { ["UHID:" + SeedIds.Hospital] = 1253, [$"INVOICE:{SeedIds.Branch}:{now.Year}"] = 7, [$"ADMISSION:{SeedIds.Branch}:{now.Year}"] = 2, [$"DISPENSE:{SeedIds.Branch}:{now.Year}"] = 0, [$"LAB:{SeedIds.Branch}:{now.Year}"] = 3, ["LAB_BARCODE:" + SeedIds.Branch] = 1, [$"LAB_ACCESSION:{SeedIds.Branch}:{now.Year}"] = 1, [$"RIS:{SeedIds.Branch}:{now.Year}"] = 3, ["RIS_ACCESSION:" + SeedIds.Branch] = 2, [$"OT:{SeedIds.Branch}:{now.Year}"] = 2, [$"ER:{SeedIds.Branch}:{now.Year}"] = 2, [$"CROSSMATCH:{SeedIds.Branch}:{now.Year}"] = 1, [$"BLOOD:{SeedIds.Branch}:{now.Year}"] = 0 };
        var nurseId = Guid.Parse("30000000-0000-0000-0000-000000000004"); var pharmacistId = Guid.Parse("30000000-0000-0000-0000-000000000005"); var labTechId = Guid.Parse("30000000-0000-0000-0000-000000000006"); var pathologistId = Guid.Parse("30000000-0000-0000-0000-000000000007");
        d.Beds = [.. Enumerable.Range(1, 6).Select(i => new Bed(Guid.Parse($"c0000000-0000-0000-0000-{i:D12}"), "General ward", $"G-{i:D2}", "General", i == 1 ? "Occupied" : i == 5 ? "Cleaning" : "Available", "")),
            new(Guid.Parse("c0000000-0000-0000-0000-000000000007"), "ICU", "ICU-01", "ICU", "Occupied", ""), new(Guid.Parse("c0000000-0000-0000-0000-000000000008"), "ICU", "ICU-02", "ICU", "Available", ""),
            new(Guid.Parse("c0000000-0000-0000-0000-000000000009"), "Private", "P-01", "Private", "Available", ""), new(Guid.Parse("c0000000-0000-0000-0000-000000000010"), "Private", "P-02", "Private", "Maintenance", "Preventive maintenance")];
        d.Admissions = [new(Guid.Parse("c1000000-0000-0000-0000-000000000001"), $"ADM-{now.Year}-0001", d.Patients[2].Id, d.Beds[0].Id, SeedIds.Doctor, "Active", "Persistent knee pain requiring observation and analgesia review", now.AddHours(-6), null, "", "", "") with { AdmissionType = "Elective", DiagnosisOnAdmission = "Osteoarthritis of the right knee with effusion", AttendantName = "Priya Mehta", AttendantPhone = "+91 9988776655", PayerType = "Self", ExpectedDischargeDate = DateOnly.FromDateTime(today.AddDays(2)) },
            new(Guid.Parse("c1000000-0000-0000-0000-000000000002"), $"ADM-{now.Year}-0002", d.Patients[5].Id, d.Beds[6].Id, d.Doctors[1].Id, "Active", "Skin irritation with systemic symptoms requiring inpatient review", now.AddHours(-3), null, "", "", "")];
        d.BedEvents = d.Admissions.Select(a => new BedEvent(Guid.NewGuid(), a.Id, a.BedId, "Admitted", "Available", "Occupied", SeedIds.Admin, a.AdmittedAt, a.Reason)).ToList();
        d.NursingNotes = [new(Guid.NewGuid(), d.Admissions[0].Id, d.Admissions[0].PatientId, "Assessment", "Patient comfortable, pain reviewed, fall precautions in place.", new(36.8m, 78, 124, 80, 70, 98), 800, 650, 3, "Low", nurseId, now.AddHours(-2)),
            new(Guid.NewGuid(), d.Admissions[1].Id, d.Admissions[1].PatientId, "Progress", "Observation continued; hydration encouraged and skin reviewed.", new(37.1m, 84, 118, 76, 62, 97), 1200, 900, 2, "Moderate", nurseId, now.AddMinutes(-45))];
        d.MedicationOrders = [new(Guid.Parse("c2000000-0000-0000-0000-000000000001"), d.Admissions[0].Id, d.Admissions[0].PatientId, d.Medicines[0].Id, "Paracetamol 500 mg", "1 tablet", "Oral", "Twice daily", now.AddHours(-5), now.AddDays(2), "Active", SeedIds.Doctor, now.AddHours(-5))];
        d.StockBatches = d.Medicines.Select((m, i) => new StockBatch(Guid.Parse($"c3000000-0000-0000-0000-{i + 1:D12}"), m.Id, $"SYN-BATCH-{i + 1:D3}", DateOnly.FromDateTime(now.UtcDateTime.AddDays(i == 1 ? 12 : 180)), 120 + i * 10, 2.5m + i, "Synthetic supplier", $"GRN-SYN-{i + 1:D3}", now.AddDays(-10), pharmacistId)).ToList();
        d.StockMovements = d.StockBatches.Select(b => new StockMovement(Guid.NewGuid(), b.Id, "Receipt", b.Quantity, b.Reference, pharmacistId, b.ReceivedAt)).ToList();
        d.LabTests = [new(Guid.Parse("c4000000-0000-0000-0000-000000000001"), "HB", "Hemoglobin", "Blood", "g/dL", 12, 16, 7, 20),
            new(Guid.Parse("c4000000-0000-0000-0000-000000000002"), "GLU", "Blood glucose", "Blood", "mg/dL", 70, 140, 50, 400),
            new(Guid.Parse("c4000000-0000-0000-0000-000000000003"), "CREAT", "Creatinine", "Blood", "mg/dL", 0.6m, 1.3m, null, 5),
            new(Guid.Parse("c4000000-0000-0000-0000-000000000004"), "CBC", "Complete blood count", "Blood", "panel", null, null, null, null)];
        d.LabOrders = [new(Guid.Parse("c5000000-0000-0000-0000-000000000001"), $"LAB-{now.Year}-0001", d.Patients[1].Id, d.Encounters[0].Id, null, d.LabTests[0].Id, "Urgent", "Collected", $"BC-{now.Year}-0001", $"ACC-{now.Year}-0001", SeedIds.Doctor, now.AddHours(-2), now.AddHours(-1), null, null, null, null, ""),
            new(Guid.Parse("c5000000-0000-0000-0000-000000000002"), $"LAB-{now.Year}-0002", d.Patients[4].Id, null, d.Admissions[0].Id, d.LabTests[1].Id, "Routine", "Processing", $"BC-{now.Year}-0002", $"ACC-{now.Year}-0002", d.Doctors[1].Id, now.AddHours(-3), now.AddHours(-2), now.AddHours(-1), null, null, null, ""),
            new(Guid.Parse("c5000000-0000-0000-0000-000000000003"), $"LAB-{now.Year}-0003", d.Patients[0].Id, d.Encounters[1].Id, null, d.LabTests[2].Id, "Routine", "Authorized", $"BC-{now.Year}-0003", $"ACC-{now.Year}-0003", SeedIds.Doctor, now.AddHours(-5), now.AddHours(-4), now.AddHours(-3), now.AddHours(-2), now.AddHours(-1), now.AddMinutes(-30), "")];
        d.LabResults = [new(Guid.Parse("c6000000-0000-0000-0000-000000000001"), d.LabOrders[2].Id, 1, "1.1", 1.1m, "mg/dL", 0.6m, 1.3m, "Normal", labTechId, now.AddHours(-2), labTechId == Guid.Empty ? null : Guid.Parse("30000000-0000-0000-0000-000000000008"), now.AddHours(-1), pathologistId, now.AddMinutes(-30), "synthetic-seed-hash")];
        d.CriticalAlerts = [new(Guid.NewGuid(), "Laboratory", d.LabOrders[1].Id, d.Patients[4].Id, "Blood glucose: 460 mg/dL (Critical high)", "Critical", "Open", now.AddMinutes(-20), null, null, "")];
        var radiologistId = Guid.Parse("30000000-0000-0000-0000-000000000010"); var radiologist2Id = Guid.Parse("30000000-0000-0000-0000-000000000011"); var bloodTechId = Guid.Parse("30000000-0000-0000-0000-000000000012");
        d.RadiologyOrders = [
            new(Guid.Parse("d1000000-0000-0000-0000-000000000001"), $"RIS-{now.Year}-0001", d.Patients[2].Id, null, d.Admissions[0].Id, "X-ray knee AP & lateral", "X-ray", "Knee", "Left", "Routine", "Authorized", $"ACC-R-{now.Year}-0001", "Assessment of knee pain following a fall.", SeedIds.Doctor, now.AddHours(-26), now.AddHours(-25), now.AddHours(-24), "1.2.840.113619.2.55.3.604688119.868.240000001"),
            new(Guid.Parse("d1000000-0000-0000-0000-000000000002"), $"RIS-{now.Year}-0002", d.Patients[4].Id, null, null, "Ultrasound abdomen", "Ultrasound", "Abdomen", "Not applicable", "Urgent", "Reported", $"ACC-R-{now.Year}-0002", "Right upper abdominal pain; evaluate for calculi.", d.Doctors[1].Id, now.AddHours(-8), now.AddHours(-7), now.AddHours(-6), "1.2.840.113619.2.55.3.604688119.868.240000002"),
            new(Guid.Parse("d1000000-0000-0000-0000-000000000003"), $"RIS-{now.Year}-0003", d.Patients[0].Id, d.Encounters[1].Id, null, "CT head plain", "CT", "Head", "Not applicable", "STAT", "Ordered", null, "Acute onset headache with vomiting.", SeedIds.Doctor, now.AddMinutes(-40), null, null, null)];
        d.RadiologyReports = [
            new(Guid.Parse("d2000000-0000-0000-0000-000000000001"), d.RadiologyOrders[0].Id, 1, "Joint spaces are preserved. No fracture or dislocation. Small suprapatellar effusion.", "Small left knee effusion without fracture.", false, "Authorized", radiologistId, now.AddHours(-23), radiologist2Id, now.AddHours(-22), "synthetic-seed-hash-ris-1"),
            new(Guid.Parse("d2000000-0000-0000-0000-000000000002"), d.RadiologyOrders[1].Id, 1, "Mild hydronephrosis on the right with a 6 mm calculus in the proximal ureter.", "Right ureteric calculus with mild hydronephrosis.", true, "Reported", radiologistId, now.AddHours(-5), null, null, null)];
        d.EmergencyRegistrations = [
            new(Guid.Parse("d3000000-0000-0000-0000-000000000001"), $"ER-{now.Year}-0001", d.Patients[4].Id, d.Patients[4].FullName, d.Patients[4].Sex, d.Patients[4].DateOfBirth, "Ambulance", "Open", now.AddMinutes(-75), nurseId) with { ReferringFacility = "City Nursing Home, Karol Bagh", BroughtBy = "Family · Mrs. Singh", MedicoLegalCase = false },
            new(Guid.Parse("d3000000-0000-0000-0000-000000000002"), $"ER-{now.Year}-0002", null, "Unknown male", "Unknown", null, "Police", "Dispositioned", now.AddHours(-5), SeedIds.Admin) with { ReferringFacility = "", BroughtBy = "Police escort · PCR van 42", MedicoLegalCase = true }];
        d.TriageAssessments = [
            new(Guid.NewGuid(), d.EmergencyRegistrations[0].Id, "Red", "Chest pain with breathlessness and sweating.", new(37.2m, 118, 92, 58, 82, 91), nurseId, now.AddMinutes(-70)),
            new(Guid.NewGuid(), d.EmergencyRegistrations[1].Id, "Yellow", "Head injury after a road accident; conscious and oriented.", new(36.9m, 96, 128, 80, 76, 97), nurseId, now.AddHours(-4.5))];
        d.EmergencyObservations = [new(Guid.NewGuid(), d.EmergencyRegistrations[0].Id, "Cardiac monitor attached; IV access secured; ECG obtained.", new(37.1m, 116, 94, 60, 82, 93), nurseId, now.AddMinutes(-55))];
        d.EmergencyDispositions = [new(Guid.NewGuid(), d.EmergencyRegistrations[1].Id, "Discharge", "Wound dressed, tetanus booster given, advised observation at home.", null, null, SeedIds.Doctor, now.AddHours(-4))];
        d.OtBookings = [
            new(Guid.Parse("d4000000-0000-0000-0000-000000000001"), $"OT-{now.Year}-0001", d.Patients[2].Id, d.Admissions[0].Id, "Arthroscopic partial meniscectomy", "Left", "Knee", d.Doctors[2].Id, "Regional", "Elective", "Scheduled", "OT-1", new DateTimeOffset(today.AddDays(1).AddHours(9), TimeSpan.Zero), new DateTimeOffset(today.AddDays(1).AddHours(10), TimeSpan.Zero), now.AddHours(-20), SeedIds.Doctor, null, null, ""),
            new(Guid.Parse("d4000000-0000-0000-0000-000000000002"), $"OT-{now.Year}-0002", d.Patients[6].Id, null, "Incision and drainage of forearm abscess", "Right", "Forearm", d.Doctors[2].Id, "Local", "Urgent", "Requested", "", null, null, now.AddHours(-2), SeedIds.Doctor, null, null, "")];
        d.OtChecklists = [new(Guid.NewGuid(), d.OtBookings[0].Id, true, true, true, true, true, "Checklist completed at the pre-operative huddle.", nurseId, now.AddHours(-1))];
        d.BloodUnits = [
            new(Guid.Parse("d5000000-0000-0000-0000-000000000001"), "NB-2401", "O-", "PRBC", DateOnly.FromDateTime(today.AddDays(28)), "Reserved", "Voluntary donor", now.AddDays(-7)),
            new(Guid.Parse("d5000000-0000-0000-0000-000000000002"), "NB-2402", "B+", "PRBC", DateOnly.FromDateTime(today.AddDays(24)), "Available", "Voluntary donor", now.AddDays(-11)),
            new(Guid.Parse("d5000000-0000-0000-0000-000000000003"), "NB-2403", "A+", "FFP", DateOnly.FromDateTime(today.AddDays(180)), "Quarantined", "Replacement donor", now.AddHours(-8)),
            new(Guid.Parse("d5000000-0000-0000-0000-000000000004"), "NB-2404", "AB+", "Platelets", DateOnly.FromDateTime(today.AddDays(3)), "Discarded", "Voluntary donor", now.AddDays(-2))];
        d.BloodScreenings = [
            new(Guid.NewGuid(), d.BloodUnits[0].Id, false, false, false, false, "All markers non-reactive.", bloodTechId, now.AddDays(-7).AddHours(2)),
            new(Guid.NewGuid(), d.BloodUnits[1].Id, false, false, false, false, "All markers non-reactive.", bloodTechId, now.AddDays(-11).AddHours(2)),
            new(Guid.NewGuid(), d.BloodUnits[3].Id, true, false, false, false, "HBsAg reactive — unit discarded.", bloodTechId, now.AddDays(-2).AddHours(3))];
        d.Crossmatches = [new(Guid.NewGuid(), $"XM-{now.Year}-0001", d.BloodUnits[0].Id, d.Patients[4].Id, "Compatible", "Matched", bloodTechId, now.AddMinutes(-90))];
        d.Policies["nursing.escalation"] = System.Text.Json.JsonSerializer.Serialize(new NursingEscalationPolicy(90, 90, 180, 50, 130, 39.5m, 8));
        d.Audit = [new(Guid.NewGuid(), SeedIds.Admin, "Anjali Sharma", "patient.registered", "Patient", d.Patients[0].Id, "Synthetic preview seed", now.AddMinutes(8)), new(Guid.NewGuid(), SeedIds.Admin, "Anjali Sharma", "appointment.booked", "Appointment", d.Appointments[2].Id, "Synthetic preview seed", now.AddMinutes(12)), new(Guid.NewGuid(), SeedIds.Doctor, d.Doctors[0].Name, "encounter.signed", "Encounter", historical.Id, "Synthetic preview seed", now.AddMinutes(18)), new(Guid.NewGuid(), SeedIds.Admin, "Amit Joshi", "invoice.payment.recorded", "Invoice", d.Invoices[0].Id, "Synthetic preview seed", now.AddMinutes(24))];
        d.Payers =
        [
            new(Guid.Parse("f0000000-0000-0000-0000-000000000001"), "STAR", "Star Health Insurance", "claims@starhealth.example", true),
            new(Guid.Parse("f0000000-0000-0000-0000-000000000002"), "NIVA", "Niva Bupa Health Insurance", "tpa@nivabupa.example", true)
        ];
        d.InsurancePlans =
        [
            new(Guid.Parse("f1000000-0000-0000-0000-000000000001"), d.Payers[0].Id, "SH-FAM", "Star Family Health Cover", "Family floater", 500000),
            new(Guid.Parse("f1000000-0000-0000-0000-000000000002"), d.Payers[1].Id, "NB-RAF", "ReAssure Family Floater", "Family floater", 300000)
        ];
        d.Coverages =
        [
            new(Guid.Parse("e1000000-0000-0000-0000-000000000001"), $"COV-{now.Year}-0001", d.Patients[0].Id, d.InsurancePlans[0].Id, $"POL-STAR-{now.Year}-88121", "MBR-008121", new DateOnly(now.Year, 1, 1), new DateOnly(now.Year, 12, 31), 500000, 5000, "Active"),
            new(Guid.Parse("e1000000-0000-0000-0000-000000000002"), $"COV-{now.Year}-0002", d.Patients[1].Id, d.InsurancePlans[1].Id, $"POL-NIVA-{now.Year}-45210", "MBR-045210", new DateOnly(now.Year, 1, 1), new DateOnly(now.Year, 12, 31), 300000, 2500, "Active")
        ];
        d.InventoryStores =
        [
            new(Guid.Parse("e2000000-0000-0000-0000-000000000001"), "CMS", "Central Medical Store", "Central"),
            new(Guid.Parse("e2000000-0000-0000-0000-000000000002"), "OTS", "Theatre Store", "Department")
        ];
        d.InventoryItems =
        [
            new(Guid.Parse("e3000000-0000-0000-0000-000000000001"), "CON-001", "Gloves, sterile nitrile (box of 100)", "Consumable", "Box", 20, 850, true),
            new(Guid.Parse("e3000000-0000-0000-0000-000000000002"), "CON-002", "IV cannula 20G", "Consumable", "Piece", 50, 40, true),
            new(Guid.Parse("e3000000-0000-0000-0000-000000000003"), "AST-001", "Syringe pump", "Asset", "Unit", 2, 42000, true)
        ];
        d.Vendors =
        [
            new(Guid.Parse("e4000000-0000-0000-0000-000000000001"), "VND-01", "MediCore Supplies Pvt Ltd", true),
            new(Guid.Parse("e4000000-0000-0000-0000-000000000002"), "VND-02", "OrthoPlus Instruments", true),
            new(Guid.Parse("e4000000-0000-0000-0000-000000000003"), "VND-03", "QuickBuy Traders", false)
        ];
        d.InventoryBatches =
        [
            new(Guid.Parse("e5000000-0000-0000-0000-000000000001"), "GLV-2601", d.InventoryItems[0].Id, d.InventoryStores[0].Id, new DateOnly(now.Year + 1, 6, 30), 400, 850),
            new(Guid.Parse("e5000000-0000-0000-0000-000000000002"), "IVL-2601", d.InventoryItems[1].Id, d.InventoryStores[0].Id, new DateOnly(now.Year + 2, 1, 31), 250, 40)
        ];
        d.InstrumentSets =
        [
            new(Guid.Parse("f6000000-0000-0000-0000-000000000001"), "CS-001", "Major Surgery Set", "Sterile", 0, now.AddDays(14), null),
            new(Guid.Parse("f6000000-0000-0000-0000-000000000002"), "CS-002", "Minor Procedure Set", "Dirty", 1, null, null)
        ];
        d.InstrumentItems =
        [
            new(Guid.NewGuid(), d.InstrumentSets[0].Id, "Scalpel handle", "INS-001-A", "Ok"),
            new(Guid.NewGuid(), d.InstrumentSets[0].Id, "Artery forceps", "INS-001-B", "Ok"),
            new(Guid.NewGuid(), d.InstrumentSets[0].Id, "Retractor", "INS-001-C", "Ok"),
            new(Guid.NewGuid(), d.InstrumentSets[1].Id, "Tissue forceps", "INS-002-A", "Ok"),
            new(Guid.NewGuid(), d.InstrumentSets[1].Id, "Scissors", "INS-002-B", "Missing")
        ];
        // ---- Phase 5: quality, portal, reporting ---------------------------
        d.Infections =
        [
            new(Guid.Parse("f7000000-0000-0000-0000-000000000001"), "INF-SEED-0001", d.Patients[1].Id, null, "Healthcare-associated", "Surgical site", "Surveillance screening", DateOnly.FromDateTime(now.AddDays(-6).UtcDateTime), "Moderate", "Reported", "Sneha Iyer", now.AddDays(-5), "", "")
        ];
        d.Incidents =
        [
            new(Guid.Parse("f7000000-0000-0000-0000-000000000002"), "QIR-SEED-0001", "Medication", "Moderate", "Near miss: look-alike vial picked from the ward trolley and caught at the bedside check.", "Ward 2", d.Patients[2].Id, "Nisha Thomas", now.AddDays(-4), "Reported", "", "", null, "", ""),
            new(Guid.Parse("f7000000-0000-0000-0000-000000000003"), "QIR-SEED-0002", "Fall", "Severe", "Patient slipped near the bathroom door despite the wet-floor sign being placed.", "Ward 1", d.Patients[0].Id, "Amrita Nair", now.AddDays(-3), "UnderInvestigation", "Assign to ward safety owner for root-cause analysis.", "Vinay Raheja", now.AddDays(-2), "", "")
        ];
        d.Risks =
        [
            new(Guid.Parse("f7000000-0000-0000-0000-000000000004"), "RSK-SEED-0001", "Infection control", "Hand-hygiene compliance drift during peak admission hours.", 3, 4, "Increase audit frequency and buddy reminders during peak hours.", "Sneha Iyer", "Mitigating", now.AddDays(-7))
        ];
        d.Capas =
        [
            new(Guid.Parse("f7000000-0000-0000-0000-000000000005"), "CAPA-SEED-0001", d.Incidents[0].Id, null, null, null, "Quarantined the look-alike vial lot and relabelled the trolley drawer.", "Tall-man lettering on labels and a second-person check for high-alert vials.", "Ward-level storage standard with quarterly spot checks.", "Vinay Raheja", now.AddDays(25), "Open", "", false, "", null, now.AddDays(-3), "Vinay Raheja")
        ];
        d.Audits =
        [
            new(Guid.Parse("f7000000-0000-0000-0000-000000000006"), "AUD-SEED-0001", "Infection control", "Hand-hygiene moments observed across wards 1-3 per the WHO five moments.", DateOnly.FromDateTime(now.AddDays(12).UtcDateTime), "Sneha Iyer", "Planned", "", 0, "", now.AddDays(-2), "Vinay Raheja")
        ];
        d.QualityIndicators =
        [
            new(Guid.Parse("f7000000-0000-0000-0000-000000000007"), "HAI-01", "Healthcare-associated infection rate", "Confirmed HAI cases per 100 registered patients in the reporting month.", "Confirmed HAI cases in the month", "Registered patients in the month", "Monthly", "Sneha Iyer", "Active"),
            new(Guid.Parse("f7000000-0000-0000-0000-000000000008"), "IND-01", "Incident closure within 30 days", "Incidents closed within 30 days of reporting.", "Incidents closed within 30 days", "Incidents reported", "Monthly", "Vinay Raheja", "Active")
        ];
        d.IndicatorResults =
        [
            new(Guid.NewGuid(), d.QualityIndicators[0].Id, now.AddMonths(-1).ToString("yyyy-MM"), now.AddMonths(-1).ToString("yyyy-MM"), 2, 180, Math.Round(2m / 180m * 100, 2), "Surveillance log vs registration register", "Provisional month close.", "Sneha Iyer", now.AddDays(-2))
        ];
        d.QualityDocuments =
        [
            new(Guid.Parse("f7000000-0000-0000-0000-000000000009"), "DOC-SEED-0001", "Hand Hygiene SOP", "SOP", "Published", 1, "Sneha Iyer", now.AddDays(-10))
        ];
        d.DocumentVersions =
        [
            new(Guid.NewGuid(), d.QualityDocuments[0].Id, 1, "Initial issue for ward rollout.", "Wash hands before and after every patient contact using the WHO five moments. Alcohol rub for hands that are not visibly soiled; soap and water for spore-forming organisms.", "Sneha Iyer", now.AddDays(-10), "Vinay Raheja", now.AddDays(-9))
        ];
        d.SopAcknowledgments =
        [
            new(Guid.NewGuid(), d.QualityDocuments[0].Id, 1, "Nisha Thomas", "Read and understood; will apply on shift.", now.AddDays(-8))
        ];
        d.PortalAccounts =
        [
            new(Guid.Parse("f7000000-0000-0000-0000-000000000010"), Guid.Parse("30000000-0000-0000-0000-000000000020"), d.Patients[0].Id, "dolly.chauhan", "Verified", "Aadhaar + in-person desk verification at registration counter 2.", "Anjali Sharma", now.AddDays(-6), now.AddDays(-6)),
            new(Guid.Parse("f7000000-0000-0000-0000-000000000011"), Guid.Parse("30000000-0000-0000-0000-000000000021"), d.Patients[1].Id, "ramesh.gupta", "Verified", "Aadhaar + in-person desk verification at registration counter 1.", "Anjali Sharma", now.AddDays(-5), now.AddDays(-5))
        ];
        d.PatientLinks =
        [
            new(Guid.NewGuid(), d.PortalAccounts[0].Id, d.Patients[0].Id, "Self", "Active", "Aadhaar + in-person desk verification.", now.AddDays(-6)),
            new(Guid.NewGuid(), d.PortalAccounts[1].Id, d.Patients[1].Id, "Self", "Active", "Aadhaar + in-person desk verification.", now.AddDays(-5))
        ];
        d.PortalPreferences = [new(Guid.NewGuid(), d.PortalAccounts[0].Id, "SMS", "English", true), new(Guid.NewGuid(), d.PortalAccounts[1].Id, "SMS", "Hindi", true)];
        d.ReportDefinitions =
        [
            new(Guid.Parse("f7000000-0000-0000-0000-000000000011"), "OPD-VOL", "OPD volume by practitioner", "Management", "Active", 1, "Anjali Sharma", now.AddDays(-5))
        ];
        d.ReportVersions =
        [
            new(Guid.NewGuid(), d.ReportDefinitions[0].Id, 1, "opd_volume_by_doctor", "doctorId,visits,cancelled", "from,to", "Anjali Sharma", now.AddDays(-5), "Vinay Raheja", now.AddDays(-4))
        ];
        d.LabOrders = [.. d.LabOrders, new(Guid.Parse("f7000000-0000-0000-0000-000000000012"), $"LAB-{now.Year}-SEED", d.Patients[0].Id, null, null, d.LabTests[0].Id, "Routine", "Authorized", $"BC-{now.Year}-SEED", $"ACC-{now.Year}-SEED", SeedIds.Doctor, now.AddDays(-4), now.AddDays(-4).AddHours(1), now.AddDays(-4).AddHours(2), now.AddDays(-4).AddHours(3), now.AddDays(-4).AddHours(4), now.AddDays(-4).AddHours(5), "")];
        d.LabResults = [.. d.LabResults, new(Guid.Parse("f7000000-0000-0000-0000-000000000013"), Guid.Parse("f7000000-0000-0000-0000-000000000012"), 1, "13.2", 13.2m, "g/dL", 12, 16, "Normal", SeedIds.Doctor, now.AddDays(-4).AddHours(3), SeedIds.Doctor, now.AddDays(-4).AddHours(4), SeedIds.Doctor, now.AddDays(-4).AddHours(5), null)];
        return d;
    }
}
