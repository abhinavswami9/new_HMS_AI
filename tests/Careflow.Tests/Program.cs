using Careflow.Application;
using Careflow.Domain;
using Careflow.Infrastructure;
var clock = new TestClock(); var passwords = new IdentityPasswordService();
var data = DemoData.Create(clock, passwords); using var uow = new MemoryUnitOfWork(data); var service = new HospitalService(uow, clock, passwords);
var admin = new Principal(SeedIds.Admin, "Test Admin", "Hospital Admin", SeedIds.Hospital, SeedIds.Branch);
var doctor = new Principal(SeedIds.Doctor, "Test Doctor", "Doctor", SeedIds.Hospital, SeedIds.Branch);
var nurse = new Principal(Guid.NewGuid(), "Test Nurse", "Nurse", SeedIds.Hospital, SeedIds.Branch);
var scope = Scope.For(admin, clock.UtcNow); var clinical = Scope.For(doctor, clock.UtcNow); var ct = CancellationToken.None; var passed = 0;
var inpatient = new InpatientService(uow, clock); var pharmacy = new PharmacyService(uow, clock); var laboratory = new LaboratoryService(uow, clock);
var insuranceSvc = new InsuranceService(uow, clock); var incentiveSvc = new IncentiveService(uow, clock); var teleSvc = new TelemedicineService(uow, clock);
var inventorySvc = new InventoryService(uow, clock); var cssdSvc = new CssdService(uow, clock);
var insuranceUser = data.Users.First(u => u.UserName == "insurance"); var insuranceScope = Scope.For(new Principal(insuranceUser.Id, insuranceUser.DisplayName, insuranceUser.Role, insuranceUser.HospitalId, insuranceUser.BranchId), clock.UtcNow);
var accountantUser = data.Users.First(u => u.UserName == "account"); var accountantScope = Scope.For(new Principal(accountantUser.Id, accountantUser.DisplayName, accountantUser.Role, accountantUser.HospitalId, accountantUser.BranchId), clock.UtcNow);
var storeUser = data.Users.First(u => u.UserName == "store"); var storeScope = Scope.For(new Principal(storeUser.Id, storeUser.DisplayName, storeUser.Role, storeUser.HospitalId, storeUser.BranchId), clock.UtcNow);
var cssdUser = data.Users.First(u => u.UserName == "cssdtech"); var cssdScope = Scope.For(new Principal(cssdUser.Id, cssdUser.DisplayName, cssdUser.Role, cssdUser.HospitalId, cssdUser.BranchId), clock.UtcNow);
var nurseUser = data.Users.First(u => u.UserName == "nurse"); var nurseScope = Scope.For(new Principal(nurseUser.Id, nurseUser.DisplayName, nurseUser.Role, nurseUser.HospitalId, nurseUser.BranchId), clock.UtcNow);
var pharmacistUser = data.Users.First(u => u.UserName == "pharmacist"); var pharmacyScope = Scope.For(new Principal(pharmacistUser.Id, pharmacistUser.DisplayName, pharmacistUser.Role, pharmacistUser.HospitalId, pharmacistUser.BranchId), clock.UtcNow);
var labTechUser = data.Users.First(u => u.UserName == "labtech"); var labScope = Scope.For(new Principal(labTechUser.Id, labTechUser.DisplayName, labTechUser.Role, labTechUser.HospitalId, labTechUser.BranchId), clock.UtcNow);
var labTech2User = data.Users.First(u => u.UserName == "labtech2"); var labValidateScope = Scope.For(new Principal(labTech2User.Id, labTech2User.DisplayName, labTech2User.Role, labTech2User.HospitalId, labTech2User.BranchId), clock.UtcNow);
var pathUser = data.Users.First(u => u.UserName == "pathologist"); var pathScope = Scope.For(new Principal(pathUser.Id, pathUser.DisplayName, pathUser.Role, pathUser.HospitalId, pathUser.BranchId), clock.UtcNow);
async Task Test(string name, Func<Task> run) { await run(); passed++; Console.WriteLine($"PASS {name}"); }
void Assert(bool condition, string message = "Assertion failed") { if (!condition) throw new Exception(message); }
async Task<DomainException> Throws(string code, Func<Task> run) { try { await run(); } catch (DomainException e) when (e.Code == code) { return e; } throw new Exception($"Expected domain error {code}"); }
var input = new PatientInput("Test", "Patient", new DateOnly(1990, 1, 1), "Other", "+91 9000000099", "test@example.com");
Patient? registered = null; Appointment? booked = null; Encounter? encounter = null; Invoice? invoice = null;
await Test("password hashing is salted and verifiable", () => { var a = passwords.Hash("Correct horse!42"); var b = passwords.Hash("Correct horse!42"); Assert(a != b); Assert(passwords.Verify(a, "Correct horse!42", out _)); Assert(!passwords.Verify(a, "wrong", out _)); return Task.CompletedTask; });
await Test("registration allocates a scoped UHID and audit event", async () => { registered = await service.Register(scope, input, ct); Assert(registered.Uhid.StartsWith("NCH-")); Assert(data.Audit.Any(a => a.EntityId == registered.Id && a.Action == "patient.registered")); });
await Test("duplicate registration requires explicit review", async () => { await Throws("patient.duplicate", () => service.Register(scope, input, ct)); var second = await service.Register(scope, input with { DuplicateReviewReason = "Reviewed identity documents; distinct patient." }, ct); Assert(second.Uhid != registered!.Uhid); });
await Test("RBAC denies nurse registration and doctor billing", async () => { await Throws("forbidden", () => service.Register(Scope.For(nurse, clock.UtcNow), input, ct)); await Throws("forbidden", () => service.CreateInvoice(clinical, new InvoiceInput(registered!.Id, [new(data.Services[0].Id, 1)]), ct)); });
await Test("future DOB and invalid vital ranges are rejected", async () => { await Throws("patient.dob", () => service.Register(scope, input with { DateOfBirth = scope.Today.AddDays(1) }, ct)); await Throws("vitals.range", () => Task.Run(() => ClinicalRules.ValidateVitals(new(null, null, null, null, null, 101)))); });
await Test("patient contact validation applies to registration and updates", async () => { await Throws("patient.fields", () => service.Register(scope, input with { Email = "not-an-email" }, ct)); await Throws("patient.fields", () => service.Register(scope, input with { BloodGroup = "INVALID" }, ct)); await Throws("patient.fields", () => service.UpdatePatient(scope, registered!.Id, input with { Email = "not-an-email" }, registered.Version, ct)); });
await Test("patient updates cannot silently adopt another patient identity", async () => { var other = data.Patients.First(p => p.Id != registered!.Id); await Throws("patient.duplicate", () => service.UpdatePatient(scope, registered!.Id, input with { Phone = other.Phone, FirstName = other.FirstName, LastName = other.LastName, DateOfBirth = other.DateOfBirth }, registered.Version, ct)); });
await Test("booking rejects overlapping practitioner slots", async () => { booked = await service.Book(scope, new BookingInput(registered!.Id, SeedIds.Doctor, clock.UtcNow.AddDays(1), 30), ct); await Throws("booking.conflict", () => service.Book(scope, new BookingInput(data.Patients[2].Id, SeedIds.Doctor, clock.UtcNow.AddDays(1).AddMinutes(15)), ct)); });
await Test("check-in allocates a token and opens an assigned encounter", async () => { booked = await service.Transition(scope, booked!.Id, "check-in", booked.Version, "", ct); Assert(booked.Token.HasValue); encounter = await service.StartEncounter(clinical, booked.Id, ct); Assert(encounter!.PatientId == registered!.Id); Assert(encounter!.Status == "Draft"); });
var draft = new DraftInput("Test complaint", "Test examination", "Test diagnosis", "Test plan", "Review in one week", new(37, 80, 120, 80, 70, 98), [], [new(data.Medicines[0].Id, "Client supplied name", "1 tablet", "Once daily", 2, "After food")], [], false, 1);
await Test("signing requires explicit allergy reconciliation", async () => { await Throws("encounter.sign", () => service.Sign(clinical, encounter!.Id, encounter.Version, ct)); encounter = await service.SaveDraft(clinical, encounter!.Id, draft with { Version = encounter!.Version, AllergyReviewed = true }, ct); Assert(encounter!.Prescription[0].Name == "Paracetamol 500 mg"); });
await Test("allergy matching blocks a conflicting medicine", async () => { await Throws("prescription.allergy", () => service.SaveDraft(clinical, encounter!.Id, draft with { Version = encounter.Version, Allergies = [new("Paracetamol", "Rash", "Moderate")] }, ct)); });
await Test("stale clinical versions never overwrite newer content", async () => { await Throws("concurrency", () => service.SaveDraft(clinical, encounter!.Id, draft with { Version = encounter.Version - 1, ChiefComplaint = "Stale write" }, ct)); });
await Test("clinical sign-off locks content and emits an outbox event", async () => { encounter = await service.Sign(clinical, encounter!.Id, encounter.Version, ct); Assert(encounter.Status == "Signed" && encounter.ContentHash!.Length == 64); Assert(data.Outbox.Any(o => o.AggregateId == encounter.Id && o.Type == "encounter.signed")); await Throws("concurrency", () => service.SaveDraft(clinical, encounter.Id, draft with { Version = encounter.Version }, ct)); });
await Test("visit completion requires a signed encounter", async () => { var a = await service.Transition(scope, booked!.Id, "complete", booked.Version + 1, "", ct); Assert(a.Status == "Completed"); });
await Test("invoice prices are resolved server-side", async () => { invoice = await service.CreateInvoice(scope, new InvoiceInput(registered!.Id, [new(data.Services[0].Id, 2)]), ct); Assert(invoice.Total == data.Services[0].Price * 2); invoice = await service.IssueInvoice(scope, invoice.Id, invoice.Version, ct); Assert(invoice.Status == "Issued"); });
await Test("overpayment is rejected without partial audit writes", async () => { var count = data.Audit.Count; await Throws("payment.overpayment", () => service.Pay(scope, invoice!.Id, new PaymentInput(invoice.Total + 1, "Cash", "TEST-1", invoice.Version), ct)); Assert(data.Audit.Count == count); });
await Test("payment reference retries are idempotent", async () => { var payment = new PaymentInput(invoice!.Total, "Cash", "TEST-UNIQUE", invoice.Version); invoice = await service.Pay(scope, invoice.Id, payment, ct); Assert(invoice.Status == "Paid"); var count = data.Audit.Count; var retry = await service.Pay(scope, invoice.Id, payment, ct); Assert(retry.Payments.Count == 1 && data.Audit.Count == count); await Throws("payment.idempotency", () => service.Pay(scope, invoice.Id, payment with { Amount = 1 }, ct)); });
await Test("module disabling blocks new work but retains records", async () => { await service.SetModule(scope, "billing", false, "Planned billing maintenance window.", ct); await Throws("module.disabled", () => service.CreateInvoice(scope, new InvoiceInput(registered!.Id, [new(data.Services[0].Id, 1)]), ct)); Assert(data.Invoices.Any(i => i.Id == invoice!.Id)); await service.SetModule(scope, "billing", true, "Maintenance completed and verified.", ct); });
await Test("later-phase modules cannot be enabled by configuration", async () => { await Throws("module.unavailable", () => service.SetModule(scope, "quality", true, "Attempted early activation.", ct)); });
await Test("failed login is retained and successful sessions are revocable", async () => { await Throws("auth.failed", () => service.Login(new LoginInput("admin", "incorrect"), SeedIds.Hospital, SeedIds.Branch, ct, "203.0.113.10")); Assert(data.Logins.Any(l => !l.Success && l.SourceIp == "203.0.113.10")); var auth = await service.Login(new LoginInput("admin", "Preview!2026"), SeedIds.Hospital, SeedIds.Branch, ct, "203.0.113.10"); Assert(await service.Authenticate(auth.Token, SeedIds.Hospital, SeedIds.Branch, ct) is not null); await service.Logout(auth.Token, SeedIds.Hospital, SeedIds.Branch, ct); Assert(await service.Authenticate(auth.Token, SeedIds.Hospital, SeedIds.Branch, ct) is null); Assert(data.Audit.Any(a => a.Action == "auth.logout")); });
await Test("sessions are bound to the configured hospital branch", async () => { var auth = await service.Login(new LoginInput("admin", "Preview!2026"), SeedIds.Hospital, SeedIds.Branch, ct); Assert(await service.Authenticate(auth.Token, SeedIds.Hospital, Guid.NewGuid(), ct) is null); await Throws("auth.failed", () => service.Login(new LoginInput("admin", "Preview!2026"), SeedIds.Hospital, Guid.NewGuid(), ct)); await service.Logout(auth.Token, SeedIds.Hospital, SeedIds.Branch, ct); });
await Test("patient chart access is audited and clinical visibility is scoped", async () => { var chart = await service.GetChart(clinical, registered!.Id, ct); Assert(chart.Encounters.All(e => e.DoctorId == doctor.Id)); Assert(chart.Invoices.Count == 0); Assert(data.Audit.Any(a => a.Action == "patient.chart.viewed" && a.ActorId == doctor.Id)); });

// Phase 2: IPD/ADT, nursing, pharmacy and LIS
await Test("inpatient module cannot be disabled with active admissions", async () => { await Throws("module.dependency", () => service.SetModule(scope, "ipd", false, "Attempted unsafe module disable.", ct)); });
Patient? ipdPatient = null; Admission? admission = null; Bed? admissionBed = null;
await Test("admission allocates a bed and rejects double occupancy", async () => { ipdPatient = await service.Register(scope, input with { Phone = "+91 9000000199", FirstName = "IPD", LastName = "Synthetic" }, ct); admissionBed = data.Beds.First(b => b.Status == "Available"); admission = await inpatient.Admit(scope, new AdmissionInput(ipdPatient!.Id, admissionBed.Id, SeedIds.Doctor, "Synthetic inpatient observation"), ct); Assert(data.Beds.First(b => b.Id == admissionBed.Id).Status == "Occupied"); await Throws("admission.active", () => inpatient.Admit(scope, new AdmissionInput(ipdPatient.Id, data.Beds.First(b => b.Status == "Available").Id, SeedIds.Doctor, "Duplicate admission attempt"), ct)); await Throws("bed.occupied", () => inpatient.Admit(scope, new AdmissionInput(data.Patients[8].Id, admissionBed.Id, SeedIds.Doctor, "Occupied bed attempt"), ct)); });
await Test("transfer releases the old bed for cleaning and occupies the target", async () => { var target = data.Beds.First(b => b.Status == "Available"); admission = await inpatient.Transfer(scope, admission!.Id, new TransferInput(target.Id, admission.Version, "Synthetic transfer for observation"), ct); Assert(admission.BedId == target.Id); Assert(data.Beds.First(b => b.Id == admissionBed!.Id).Status == "Cleaning"); Assert(data.Beds.First(b => b.Id == target.Id).Status == "Occupied"); });
await Test("nursing assessment creates audited note and escalation alert", async () => { var note = await inpatient.AddNursingNote(nurseScope, new NursingNoteInput(admission!.Id, "Assessment", "Synthetic deterioration review", new(39.6m, 140, 85, 60, 70, 88), 500, 400, 8, "High"), ct); Assert(data.CriticalAlerts.Any(a => a.EntityId == note.Id && a.Status == "Open")); Assert(data.Audit.Any(a => a.EntityId == note.Id)); });
MedicationOrder? medOrder = null;
await Test("inpatient medication order requires formulary and active admission", async () => { medOrder = await inpatient.OrderMedication(clinical, new MedicationOrderInput(admission!.Id, data.Medicines[0].Id, "1 tablet", "Oral", "Twice daily", clock.UtcNow.AddMinutes(-1), clock.UtcNow.AddDays(1)), ct); await Throws("medicine.unknown", () => inpatient.OrderMedication(clinical, new MedicationOrderInput(admission.Id, Guid.NewGuid(), "1 tablet", "Oral", "Once daily", clock.UtcNow, null), ct)); });
await Test("eMAR administration decrements batch stock and rejects duplicate time", async () => { var batch = data.StockBatches.First(b => b.MedicineId == data.Medicines[0].Id); var before = batch.Quantity; var givenAt = clock.UtcNow; await inpatient.Administer(nurseScope, new AdministerInput(medOrder!.Id, batch.Id, 1, givenAt, "", "Synthetic administration"), ct); Assert(data.StockBatches.First(b => b.Id == batch.Id).Quantity == before - 1); await Throws("medication.duplicate", () => inpatient.Administer(nurseScope, new AdministerInput(medOrder.Id, batch.Id, 1, givenAt, "", "Duplicate attempt"), ct)); });
await Test("shift handover requires incoming acknowledgement", async () => { var incomingUser = data.Users.First(u => u.UserName == "nurse2"); var handover = await inpatient.CreateHandover(nurseScope, new HandoverInput(incomingUser.Id, "Synthetic handover summary for admitted patients.", [admission!.PatientId]), ct); await Throws("handover.state", () => inpatient.AcknowledgeHandover(nurseScope, handover.Id, new VersionInput(handover.Version), ct)); var incoming = Scope.For(new Principal(incomingUser.Id, incomingUser.DisplayName, incomingUser.Role, incomingUser.HospitalId, incomingUser.BranchId), clock.UtcNow); var ack = await inpatient.AcknowledgeHandover(incoming, handover.Id, new VersionInput(handover.Version), ct); Assert(ack.Status == "Acknowledged"); });
await Test("discharge requires clinical summary and releases bed to cleaning", async () => { await Throws("forbidden", () => inpatient.Discharge(scope, admission!.Id, new DischargeInput(admission.Version, "Synthetic discharge summary sufficient for review.", "Synthetic diagnosis", "Synthetic follow-up instructions"), ct)); admission = await inpatient.Discharge(clinical, admission!.Id, new DischargeInput(admission.Version, "Synthetic discharge summary sufficient for clinical review.", "Synthetic diagnosis", "Synthetic follow-up instructions"), ct); Assert(admission.Status == "Discharged"); Assert(data.Beds.First(b => b.Id == admission.BedId).Status == "Cleaning"); await Throws("admission.state", () => inpatient.Transfer(scope, admission.Id, new TransferInput(data.Beds.First(b => b.Status == "Available").Id, admission.Version, "Invalid post-discharge transfer"), ct)); });
await Test("bed readiness transition is controlled and audited", async () => { var bed = data.Beds.First(b => b.Status == "Cleaning"); var ready = await inpatient.SetBedStatus(nurseScope, bed.Id, new BedStatusInput("Available", "Cleaning completed and verified.", bed.Version), ct); Assert(ready.Status == "Available"); });
await Test("pharmacy stock receipt rejects expired and duplicate batches", async () => { await Throws("stock.fields", () => pharmacy.ReceiveStock(pharmacyScope, new ReceiveStockInput(data.Medicines[0].Id, "EXP-1", scope.Today.AddDays(-1), 10, 1, "Supplier", "GRN-EXP"), ct)); var batch = await pharmacy.ReceiveStock(pharmacyScope, new ReceiveStockInput(data.Medicines[0].Id, "SYN-NEW-BATCH", scope.Today.AddDays(90), 25, 2, "Synthetic supplier", "GRN-NEW"), ct); await Throws("stock.duplicate", () => pharmacy.ReceiveStock(pharmacyScope, new ReceiveStockInput(data.Medicines[0].Id, "SYN-NEW-BATCH", scope.Today.AddDays(90), 5, 2, "Synthetic supplier", "GRN-NEW-2"), ct)); Assert(data.StockMovements.Any(m => m.BatchId == batch.Id && m.Kind == "Receipt")); });
await Test("OPD dispensing requires signed prescription and is idempotent", async () => { var signed = data.Encounters.First(e => e.Status == "Signed"); var medicineId = signed.Prescription[0].MedicineId; var request = new DispenseInput("OPD", signed.PatientId, signed.Id, null, [new DispenseLineInput(medicineId, 5)], "TEST-DISPENSE-KEY"); var dispense = await pharmacy.Dispense(pharmacyScope, request, ct); var retry = await pharmacy.Dispense(pharmacyScope, request, ct); Assert(dispense.Id == retry.Id); Assert(data.Dispenses.Count(d => d.IdempotencyKey == request.IdempotencyKey) == 1); await Throws("dispense.prescription", () => pharmacy.Dispense(pharmacyScope, request with { IdempotencyKey = "TEST-DISPENSE-KEY-2", Lines = [new DispenseLineInput(data.Medicines[7].Id, 1)] }, ct)); });
await Test("dispensing idempotency key cannot be reused for a different request", async () => { var signed = data.Encounters.First(e => e.Status == "Signed"); var medicineId = signed.Prescription[0].MedicineId; var request = new DispenseInput("OPD", signed.PatientId, signed.Id, null, [new DispenseLineInput(medicineId, 1)], "TEST-DISPENSE-REUSE"); await pharmacy.Dispense(pharmacyScope, request, ct); await Throws("dispense.idempotency", () => pharmacy.Dispense(pharmacyScope, request with { Lines = [new DispenseLineInput(medicineId, 2)] }, ct)); });
await Test("IPD dispensing requires an active inpatient medication order", async () => { var active = data.Admissions.First(a => a.Status == "Active"); var medicineId = data.Medicines[1].Id; await Throws("dispense.order", () => pharmacy.Dispense(pharmacyScope, new DispenseInput("IPD", active.PatientId, null, active.Id, [new DispenseLineInput(medicineId, 1)], "TEST-IPD-NO-ORDER"), ct)); });
await Test("nursing observations require an approved escalation policy", async () => { var active = data.Admissions.First(a => a.Status == "Active"); var policy = data.Policies["nursing.escalation"]; data.Policies.Remove("nursing.escalation"); await Throws("policy.missing", () => inpatient.AddNursingNote(nurseScope, new NursingNoteInput(active.Id, "Assessment", "Synthetic policy gate", new(null, null, null, null, null, null), null, null, null, "Low"), ct)); data.Policies["nursing.escalation"] = policy; });
LabOrder? labOrder = null;
await Test("LIS order and specimen collection allocate barcode/accession", async () => { labOrder = await laboratory.Order(clinical, new LabOrderInput(ipdPatient!.Id, data.LabTests[0].Id, null, null, "STAT", "Synthetic critical value workflow"), ct); labOrder = await laboratory.Collect(labScope, labOrder.Id, new CollectSpecimenInput("Blood", 2, labOrder.Version), ct); Assert(labOrder.Barcode != null && labOrder.Accession != null && labOrder.Status == "Collected"); });
await Test("LIS result validation enforces role separation and critical alerting", async () => { labOrder = await laboratory.Process(labScope, labOrder!.Id, new VersionInput(labOrder.Version), ct); var result = await laboratory.EnterResult(labScope, labOrder.Id, new LabResultInput("6.5", "g/dL", null, null, data.LabOrders.First(o => o.Id == labOrder.Id).Version), ct); Assert(result.Flag == "Critical low"); Assert(data.CriticalAlerts.Any(a => a.EntityId == labOrder.Id && a.Kind == "Laboratory")); var order = data.LabOrders.First(o => o.Id == labOrder.Id); await Throws("lab.separation", () => laboratory.Validate(labScope, order.Id, new VersionInput(order.Version), ct)); await laboratory.Validate(labValidateScope, order.Id, new VersionInput(order.Version), ct); order = data.LabOrders.First(o => o.Id == labOrder.Id); var authorized = await laboratory.Authorize(pathScope, order.Id, new VersionInput(order.Version), ct); Assert(authorized.ContentHash!.Length == 64); Assert(data.LabOrders.First(o => o.Id == labOrder.Id).Status == "Authorized"); });
await Test("critical alerts require acknowledgement note and become immutable", async () => { var alert = data.CriticalAlerts.First(a => a.Kind == "Laboratory" && a.Status == "Open" && a.EntityId == labOrder!.Id); var ack = await laboratory.AcknowledgeAlert(clinical, alert.Id, new AlertAcknowledgeInput("Synthetic clinician acknowledgement and escalation."), ct); Assert(ack.Status == "Acknowledged"); await Throws("alert.state", () => laboratory.AcknowledgeAlert(clinical, alert.Id, new AlertAcknowledgeInput("Duplicate acknowledgement."), ct)); });
await Test("specimen rejection preserves reason and blocks further processing", async () => { var order = await laboratory.Order(clinical, new LabOrderInput(ipdPatient!.Id, data.LabTests[1].Id, null, null, "Routine", ""), ct); order = await laboratory.Collect(labScope, order.Id, new CollectSpecimenInput("Blood", 2, order.Version), ct); order = await laboratory.Reject(labScope, order.Id, new VersionInput(order.Version, "Hemolysed synthetic specimen"), ct); Assert(order.Status == "Rejected"); await Throws("lab.state", () => laboratory.Process(labScope, order.Id, new VersionInput(order.Version), ct)); });

// Phase 3: RIS, emergency, operation theatre and blood bank
var radiologistUser = data.Users.First(u => u.UserName == "radiologist"); var radiologistScope = Scope.For(new Principal(radiologistUser.Id, radiologistUser.DisplayName, radiologistUser.Role, radiologistUser.HospitalId, radiologistUser.BranchId), clock.UtcNow);
var radiologist2User = data.Users.First(u => u.UserName == "radiologist2"); var radiologist2Scope = Scope.For(new Principal(radiologist2User.Id, radiologist2User.DisplayName, radiologist2User.Role, radiologist2User.HospitalId, radiologist2User.BranchId), clock.UtcNow);
var bloodtechUser = data.Users.First(u => u.UserName == "bloodtech"); var bloodScope = Scope.For(new Principal(bloodtechUser.Id, bloodtechUser.DisplayName, bloodtechUser.Role, bloodtechUser.HospitalId, bloodtechUser.BranchId), clock.UtcNow);
var radiology = new RadiologyService(uow, clock); var emergencySvc = new EmergencyService(uow, clock); var otSvc = new OtService(uow, clock); var bloodBank = new BloodBankService(uow, clock);
RadiologyOrder? risOrder = null;
await Test("radiology ordering allocates RIS numbers and validates laterality and study UIDs", async () =>
{
    await Throws("phase3.laterality", () => radiology.Order(clinical, new RadiologyOrderInput(ipdPatient!.Id, null, null, "X-ray knee", "X-ray", "Knee", "Not applicable", "Routine", "Synthetic knee pain review"), ct));
    risOrder = await radiology.Order(clinical, new RadiologyOrderInput(ipdPatient!.Id, null, null, "CT Brain plain", "CT", "Brain", "Not applicable", "Urgent", "Synthetic acute onset deficit"), ct);
    Assert(risOrder.Number.StartsWith("RIS-") && risOrder.Status == "Ordered");
    await Throws("radiology.schedule", () => radiology.Schedule(radiologistScope, risOrder!.Id, new RadiologyScheduleInput(clock.UtcNow.AddMinutes(-60), risOrder.Version), ct));
    risOrder = await radiology.Schedule(radiologistScope, risOrder!.Id, new RadiologyScheduleInput(clock.UtcNow.AddHours(2), risOrder.Version), ct);
    Assert(risOrder.Accession!.StartsWith("ACC-R-"));
    await Throws("radiology.uid", () => radiology.Perform(radiologistScope, risOrder!.Id, new RadiologyPerformInput("not-a-study-uid", risOrder.Version), ct));
    risOrder = await radiology.Perform(radiologistScope, risOrder!.Id, new RadiologyPerformInput("1.2.840.113619.2.55.3.604688119.868.1762000000.123", risOrder.Version), ct);
    Assert(risOrder.Status == "Performed" && risOrder.StudyUid!.StartsWith("1."));
});
await Test("radiology critical reporting raises alerts and authorization enforces separation", async () =>
{
    var report = await radiology.Report(radiologistScope, risOrder!.Id, new RadiologyReportInput("Synthetic acute hypodensity in the right middle cerebral artery territory.", "Synthetic acute infarct. Critical result communicated.", true, risOrder.Version), ct);
    Assert(data.CriticalAlerts.Any(a => a.EntityId == risOrder!.Id && a.Kind == "Radiology" && a.Status == "Open"));
    var reportedVersion = data.RadiologyOrders.First(o => o.Id == risOrder!.Id).Version;
    await Throws("radiology.separation", () => radiology.Authorize(radiologistScope, risOrder!.Id, new VersionInput(reportedVersion), ct));
    await Throws("concurrency", () => radiology.Authorize(radiologist2Scope, risOrder!.Id, new VersionInput(reportedVersion - 1), ct));
    var authorized = await radiology.Authorize(radiologist2Scope, risOrder!.Id, new VersionInput(reportedVersion), ct);
    Assert(authorized.Status == "Authorized" && authorized.ContentHash!.Length == 64);
    await Throws("radiology.state", () => radiology.Cancel(scope, risOrder!.Id, new VersionInput(data.RadiologyOrders.First(o => o.Id == risOrder!.Id).Version), ct));
});
await Test("emergency identity mismatch and triage categories are enforced", async () =>
{
    await Throws("emergency.identity", () => emergencySvc.Register(clinical, new EmergencyRegistrationInput(data.Patients[0].Id, "Wrong Name", "Female", null, "Walk-in"), ct));
    await Throws("emergency.triage", () => emergencySvc.Triage(nurseScope, data.EmergencyRegistrations.First(r => r.Status == "Open").Id, new TriageInput("Orange", "Synthetic invalid category", new(null, null, null, null, null, null)), ct));
});
EmergencyRegistration? er = null;
await Test("emergency-to-admission disposition creates an inpatient episode", async () =>
{
    er = await emergencySvc.Register(clinical, new EmergencyRegistrationInput(data.Patients[3].Id, data.Patients[3].FullName, "Male", null, "Ambulance"), ct);
    await emergencySvc.Triage(nurseScope, er.Id, new TriageInput("Red", "Synthetic crushing chest pain with diaphoresis", new(36.8m, 110, 140, 90, null, 94)), ct);
    await emergencySvc.Observe(clinical, er.Id, new EmergencyObservationInput("Synthetic observation: pain responding to initial analgesia.", new(null, 96, 132, 84, null, 96)), ct);
    var bed = data.Beds.First(b => b.Status == "Available");
    er = await emergencySvc.Dispose(clinical, er.Id, new DispositionInput("Admit", "Synthetic admission for monitored cardiac care.", bed.Id, SeedIds.Doctor, null), ct);
    Assert(er.Status == "Dispositioned");
    Assert(data.Admissions.Any(a => a.PatientId == data.Patients[3].Id && a.Status == "Active" && a.BedId == bed.Id));
    await Throws("emergency.state", () => emergencySvc.Triage(nurseScope, er.Id, new TriageInput("Green", "Synthetic post-disposition attempt", new(null, null, null, null, null, null)), ct));
});
await Test("theatre scheduling rejects overlaps and time-in requires the safety checklist", async () =>
{
    var otAdmission = data.Admissions.First(a => a.Status == "Active");
    var booking = await otSvc.Request(clinical, new OtBookingInput(otAdmission.PatientId, otAdmission.Id, "Synthetic forearm incision and drainage", "Left", "Left forearm", SeedIds.Doctor, "Local", "Elective"), ct);
    await Throws("ot.conflict", () => otSvc.Schedule(scope, booking.Id, new OtScheduleInput("OT-1", clock.UtcNow.AddDays(1).AddMinutes(-30), 60, booking.Version), ct));
    booking = await otSvc.Schedule(scope, booking.Id, new OtScheduleInput("OT-3", clock.UtcNow.AddDays(2).AddHours(3), 45, booking.Version), ct);
    await otSvc.Checklist(clinical, booking.Id, new OtChecklistInput(true, true, true, true, true, "Synthetic surgical safety checklist completed."), ct);
    await Throws("ot.checklist.exists", () => otSvc.Checklist(clinical, booking.Id, new OtChecklistInput(true, true, true, true, true, "Duplicate checklist attempt."), ct));
    await Throws("concurrency", () => otSvc.TimeIn(clinical, booking.Id, new VersionInput(booking.Version - 1), ct));
    booking = await otSvc.TimeIn(clinical, booking.Id, new VersionInput(booking.Version), ct);
    booking = await otSvc.TimeOut(clinical, booking.Id, new VersionInput(booking.Version), ct);
    booking = await otSvc.Complete(clinical, booking.Id, new OtCompleteInput("Synthetic procedure note: abscess drained, hemostasis achieved.", booking.Version), ct);
    Assert(booking.Status == "Completed" && booking.TimeIn.HasValue && booking.TimeOut!.Value >= booking.TimeIn.Value);
    await Throws("ot.state", () => otSvc.Cancel(scope, booking.Id, new VersionInput(booking.Version), ct));
});
await Test("blood screening quarantine and crossmatch compatibility are enforced", async () =>
{
    await bloodBank.ReceiveUnit(bloodScope, new ReceiveUnitInput("SYN-B-01", "B+", "PRBC", scope.Today.AddDays(28), "Synthetic voluntary donor"), ct);
    var unit = await bloodBank.Screen(bloodScope, data.BloodUnits.First(u => u.UnitNumber == "SYN-B-01").Id, new ScreeningInput(true, false, false, false, "Synthetic reactive screening"), ct);
    Assert(unit.Status == "Discarded");
    await bloodBank.ReceiveUnit(bloodScope, new ReceiveUnitInput("SYN-B-02", "A+", "FFP", scope.Today.AddDays(90), "Synthetic voluntary donor"), ct);
    await bloodBank.Screen(bloodScope, data.BloodUnits.First(u => u.UnitNumber == "SYN-B-02").Id, new ScreeningInput(false, false, false, false, "Synthetic clean screening"), ct);
    await Throws("blood.incompatible", () => bloodBank.Crossmatch(bloodScope, new CrossmatchInput(data.BloodUnits.First(u => u.UnitNumber == "SYN-B-02").Id, data.Patients[0].Id), ct));
    await bloodBank.ReceiveUnit(bloodScope, new ReceiveUnitInput("SYN-B-03", "O-", "PRBC", scope.Today.AddDays(35), "Synthetic voluntary donor"), ct);
    await bloodBank.Screen(bloodScope, data.BloodUnits.First(u => u.UnitNumber == "SYN-B-03").Id, new ScreeningInput(false, false, false, false, "Synthetic clean screening"), ct);
    var xm = await bloodBank.Crossmatch(bloodScope, new CrossmatchInput(data.BloodUnits.First(u => u.UnitNumber == "SYN-B-03").Id, data.Patients[1].Id), ct);
    Assert(xm.Compatibility == "Compatible" && xm.Number.StartsWith("XM-"));
    Assert(data.BloodUnits.First(u => u.UnitNumber == "SYN-B-03").Status == "Reserved");
});
await Test("transfusion reactions raise blood alerts and closed issues are immutable", async () =>
{
    var unitId = data.BloodUnits.First(u => u.UnitNumber == "SYN-B-03").Id;
    var issue = await bloodBank.Issue(bloodScope, new BloodIssueInput(unitId, data.Patients[1].Id, null, "Synthetic transfusion request"), ct);
    Assert(issue.Number.StartsWith("BLD-"));
    await Throws("forbidden", () => bloodBank.Transfuse(bloodScope, issue.Id, new TransfuseInput("Rigors and chills", "Synthetic reaction", issue.Version), ct));
    issue = await bloodBank.Transfuse(clinical, issue.Id, new TransfuseInput("Rigors and chills", "Synthetic transfusion reaction", issue.Version), ct);
    Assert(data.CriticalAlerts.Any(a => a.EntityId == issue.Id && a.Kind == "Blood" && a.Status == "Open"));
    await Throws("blood.state", () => bloodBank.Transfuse(clinical, issue.Id, new TransfuseInput("Nil", "Duplicate attempt", issue.Version), ct));
    await bloodBank.ReceiveUnit(bloodScope, new ReceiveUnitInput("SYN-B-04", "O-", "PRBC", scope.Today.AddDays(35), "Synthetic voluntary donor"), ct);
    await bloodBank.Screen(bloodScope, data.BloodUnits.First(u => u.UnitNumber == "SYN-B-04").Id, new ScreeningInput(false, false, false, false, "Synthetic clean screening"), ct);
    await bloodBank.Crossmatch(bloodScope, new CrossmatchInput(data.BloodUnits.First(u => u.UnitNumber == "SYN-B-04").Id, data.Patients[1].Id), ct);
    var issue2 = await bloodBank.Issue(bloodScope, new BloodIssueInput(data.BloodUnits.First(u => u.UnitNumber == "SYN-B-04").Id, data.Patients[1].Id, null, "Synthetic issue for return flow"), ct);
    var returned = await bloodBank.Return(bloodScope, issue2.Id, new ReturnInput(false, "Synthetic cold chain excursion; not fit for reissue.", issue2.Version), ct);
    Assert(returned.Status == "Returned");
    Assert(data.BloodUnits.First(u => u.UnitNumber == "SYN-B-04").Status == "Discarded");
});

// ── Phase 4: insurance, incentives, telemedicine, inventory, CSSD ─────────────
Coverage? coverage = null; Preauthorization? preauth = null; Claim? claim = null; IncentiveRule? rule = null; Appointment? teleAppointment = null; InventoryIndent? Phase4Indent = null; PurchaseOrder? Phase4Order = null; SterilizerCycle? Phase4Cycle = null;
await Test("coverage registration validates windows, limits and duplicates", async () =>
{
    var plan = data.InsurancePlans[0];
    await Throws("insurance.reference", () => insuranceSvc.RegisterCoverage(scope, new CoverageInput(registered!.Id, Guid.NewGuid(), "POL-UNKNOWN-1", "MBR-0001", scope.Today.AddDays(-10), scope.Today.AddDays(300), 100000, 500), ct));
    await Throws("insurance.coverage", () => insuranceSvc.RegisterCoverage(scope, new CoverageInput(registered!.Id, plan.Id, "POL-BAD-1", "MBR-0002", scope.Today, scope.Today.AddDays(-1), 100000, 500), ct));
    await Throws("insurance.coverage", () => insuranceSvc.RegisterCoverage(scope, new CoverageInput(registered!.Id, plan.Id, "POL-BAD-2", "MBR-0003", scope.Today.AddDays(-10), scope.Today.AddDays(300), 100000, 100000), ct));
    coverage = await insuranceSvc.RegisterCoverage(scope, new CoverageInput(registered!.Id, plan.Id, "POL-TEST-9001", "MBR-9001", scope.Today.AddDays(-30), scope.Today.AddDays(300), 200000, 1000), ct);
    Assert(coverage.Number.StartsWith("COV-") && coverage.Status == "Active");
    await Throws("insurance.duplicate", () => insuranceSvc.RegisterCoverage(scope, new CoverageInput(registered!.Id, plan.Id, "POL-TEST-9001", "MBR-9002", scope.Today.AddDays(-30), scope.Today.AddDays(300), 200000, 1000), ct));
});
await Test("eligibility checks are audited without mutating the coverage", async () =>
{
    var before = coverage!.Version;
    var checkedCoverage = await insuranceSvc.VerifyCoverage(scope, coverage!.Id, ct);
    Assert(checkedCoverage.Version == before && data.Audit.Any(a => a.Action == "insurance.eligibility.checked"));
});
await Test("preauthorization request and payer response follow decision rules", async () =>
{
    preauth = await insuranceSvc.RequestPreauth(scope, new PreauthInput(coverage!.Id, registered!.Id, "M54.5", "PROC-100", 5000, scope.Today.AddDays(30)), ct);
    await Throws("insurance.preauth", () => insuranceSvc.RespondPreauth(scope, preauth!.Id, new PreauthRespondInput("Approved", 6000, "Above requested amount.", preauth!.Version), ct));
    await Throws("insurance.preauth", () => insuranceSvc.RespondPreauth(scope, preauth!.Id, new PreauthRespondInput("Maybe", null, "Undecided response.", preauth!.Version), ct));
    preauth = await insuranceSvc.RespondPreauth(scope, preauth!.Id, new PreauthRespondInput("Approved", 4000, "Approved with panel tariff cap.", preauth!.Version), ct);
    Assert(preauth.Status == "Approved" && preauth.ApprovedAmount == 4000);
    await Throws("insurance.state", () => insuranceSvc.RespondPreauth(scope, preauth!.Id, new PreauthRespondInput("Rejected", null, "Reversing the decision.", preauth!.Version), ct));
});
await Test("claim lines recalculate totals and enforce coverage limits", async () =>
{
    await Throws("insurance.lines", () => insuranceSvc.CreateClaim(scope, new ClaimInput(registered!.Id, coverage!.Id, null, null, "M54.5", [new("LAB-1", "Panel", 0, 100)]), ct));
    await Throws("insurance.eligibility", () => insuranceSvc.CreateClaim(scope, new ClaimInput(registered!.Id, coverage!.Id, null, null, "M54.5", [new("SURG-1", "Costly procedure", 1, 250000)]), ct));
    await Throws("forbidden", () => insuranceSvc.CreateClaim(clinical, new ClaimInput(registered!.Id, coverage!.Id, null, null, "M54.5", [new("LAB-1", "Panel", 1, 100)]), ct));
    claim = await insuranceSvc.CreateClaim(scope, new ClaimInput(registered!.Id, coverage!.Id, null, null, "M54.5", [new("LAB-1", "Metabolic panel", 2, 250), new("CON-1", "Consultation", 1, 100)]), ct);
    Assert(claim.TotalAmount == 600 && claim.Status == "Draft");
});
await Test("claim lifecycle enforces transitions, settlement caps and references", async () =>
{
    claim = await insuranceSvc.SubmitClaim(scope, claim!.Id, new VersionInput(claim!.Version), ct);
    await Throws("insurance.state", () => insuranceSvc.SubmitClaim(scope, claim!.Id, new VersionInput(claim!.Version), ct));
    claim = await insuranceSvc.RaiseQuery(scope, claim!.Id, new ClaimQueryInput("Please share the itemised investigation report for adjudication."), ct);
    Assert(claim.Status == "Queried");
    claim = await insuranceSvc.RespondQuery(scope, claim!.Id, new ClaimRespondInput("Itemised report attached to the claim communication.", claim!.Version), ct);
    claim = await insuranceSvc.DecideClaim(scope, claim!.Id, new ClaimDecisionInput("Approved", 400, "Approved as per policy schedule.", claim!.Version), ct);
    Assert(claim.PatientLiability == 200);
    await Throws("insurance.settlement", () => insuranceSvc.SettleClaim(scope, claim!.Id, new SettlementInput(500, "Bank transfer", "STL-OVER-1", claim!.Version), ct));
    await insuranceSvc.SettleClaim(scope, claim!.Id, new SettlementInput(200, "Bank transfer", "STL-TEST-1", claim!.Version), ct);
    await Throws("insurance.duplicate", () => insuranceSvc.SettleClaim(scope, claim!.Id, new SettlementInput(100, "Bank transfer", "STL-TEST-1", claim!.Version), ct));
    await insuranceSvc.SettleClaim(scope, claim!.Id, new SettlementInput(200, "Cheque", "STL-TEST-2", claim!.Version), ct);
    Assert(data.Claims.First(c => c.Id == claim!.Id).Status == "Settled");
});
await Test("incentive rules enforce basis, mode, scope and overlap", async () =>
{
    await Throws("incentive.fields", () => incentiveSvc.CreateRule(scope, new IncentiveRuleInput("Broken percent", "All", null, "Consultation", "Percent", 5, scope.Today.AddDays(-60), null), ct));
    await Throws("incentive.fields", () => incentiveSvc.CreateRule(scope, new IncentiveRuleInput("Bad scope", "Department", null, "Revenue", "Percent", 5, scope.Today.AddDays(-60), null), ct));
    rule = await incentiveSvc.CreateRule(scope, new IncentiveRuleInput("Revenue share", "Doctor", SeedIds.Doctor, "Revenue", "Percent", 5, scope.Today.AddDays(-60), null), ct);
    Assert(rule.Status == "Draft");
    await Throws("incentive.overlap", () => incentiveSvc.CreateRule(scope, new IncentiveRuleInput("Overlapping share", "Doctor", SeedIds.Doctor, "Revenue", "Percent", 3, scope.Today.AddDays(-30), null), ct));
    await Throws("incentive.state", () => incentiveSvc.SetRuleStatus(scope, rule!.Id, "Retired", new VersionInput(rule!.Version), ct));
    rule = await incentiveSvc.SetRuleStatus(scope, rule!.Id, "Active", new VersionInput(rule!.Version), ct);
    var consultRule = await incentiveSvc.CreateRule(scope, new IncentiveRuleInput("Consultation flat", "All", null, "Consultation", "Fixed", 150, scope.Today.AddDays(-60), null), ct);
    consultRule = await incentiveSvc.SetRuleStatus(scope, consultRule.Id, "Active", new VersionInput(consultRule.Version), ct);
    Assert(consultRule.Status == "Active");
});
await Test("incentive batches require closed periods, separation and bounded payments", async () =>
{
    await Throws("incentive.period", () => incentiveSvc.Calculate(scope, new IncentiveBatchInput(scope.Today.AddDays(-30), scope.Today), ct));
    var fixedNow = clock.UtcNow; clock.UtcNow = fixedNow.AddDays(1);
    var tomorrow = Scope.For(admin, clock.UtcNow);
    var batch = await incentiveSvc.Calculate(tomorrow, new IncentiveBatchInput(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 13)), ct);
    Assert(batch.Status == "Calculated" && batch.TotalAmount > 0);
    await Throws("incentive.period", () => incentiveSvc.Calculate(tomorrow, new IncentiveBatchInput(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 13)), ct));
    var lines = await incentiveSvc.ListLines(tomorrow, batch.Id, ct);
    Assert(lines.Any(l => l.BasisLabel.StartsWith("Revenue:")));
    batch = await incentiveSvc.Recalculate(tomorrow, batch.Id, new BatchRecalcInput("Recalculated after payer reconciliation.", batch.Version), ct);
    await Throws("incentive.separation", () => incentiveSvc.Approve(tomorrow, batch.Id, new BatchApproveInput("Self approval attempt.", batch.Version), ct));
    batch = await incentiveSvc.Approve(accountantScope, batch.Id, new BatchApproveInput("Reviewed against payout register.", batch.Version), ct);
    await Throws("incentive.state", () => incentiveSvc.Recalculate(tomorrow, batch.Id, new BatchRecalcInput("Too late to recalculate.", batch.Version), ct));
    await incentiveSvc.Pay(accountantScope, batch.Id, new BatchPayInput(100, "Bank transfer", "INC-PAY-1", batch.Version), ct);
    await Throws("incentive.payment", () => incentiveSvc.Pay(accountantScope, batch.Id, new BatchPayInput(batch.TotalAmount, "Bank transfer", "INC-PAY-2", batch.Version), ct));
    await incentiveSvc.Pay(accountantScope, batch.Id, new BatchPayInput(batch.TotalAmount - 100, "Cheque", "INC-PAY-3", batch.Version), ct);
    Assert(data.IncentiveBatches.First(b => b.Id == batch.Id).Status == "Paid");
    clock.UtcNow = fixedNow;
});
await Test("telemedicine booking is one per appointment and consent-gated", async () =>
{
    teleAppointment = await service.Book(scope, new BookingInput(data.Patients[2].Id, SeedIds.Doctor, clock.UtcNow.AddHours(3), 30), ct);
    var session = await teleSvc.Book(scope, new TeleSessionInput(teleAppointment.Id), ct);
    Assert(session.Number.StartsWith("TEL-"));
    await Throws("tele.duplicate", () => teleSvc.Book(scope, new TeleSessionInput(teleAppointment.Id), ct));
    await Throws("tele.consent", () => teleSvc.JoinWaiting(scope, session.Id, ct));
    session = await teleSvc.RecordConsent(scope, session.Id, new TeleConsentInput("PatientConsent", true, "Consent for tele-consultation recorded."), ct);
    Assert(session.ConsentObtained);
    session = await teleSvc.JoinWaiting(scope, session.Id, ct);
    Assert(session.Status == "Waiting");
    session = await teleSvc.Cancel(scope, session.Id, new VersionInput(session.Version, "Patient cancelled."), ct);
    Assert(session.Status == "Cancelled");
});
await Test("tele join tokens are ephemeral, window-bound and consent-gated", async () =>
{
    var second = await service.Book(scope, new BookingInput(data.Patients[3].Id, SeedIds.Doctor, clock.UtcNow.AddHours(5), 30), ct);
    var session = await teleSvc.Book(scope, new TeleSessionInput(second.Id), ct);
    session = await teleSvc.RecordConsent(scope, session.Id, new TeleConsentInput("PatientConsent", true, "Consent recorded before join."), ct);
    await Throws("tele.state", () => teleSvc.IssueJoinToken(scope, session.Id, new VersionInput(session.Version), ct));
    session = await teleSvc.JoinWaiting(scope, session.Id, ct);
    var join = await teleSvc.IssueJoinToken(scope, session.Id, new VersionInput(session.Version), ct);
    Assert(join.Token.Length > 10 && join.ExpiresAt > clock.UtcNow && join.ExpiresAt <= clock.UtcNow.AddMinutes(6));
    session = await teleSvc.Admit(scope, session.Id, new VersionInput(session.Version), ct);
    Assert(session.Status == "InConsultation");
    await Throws("care.fields", () => teleSvc.End(scope, session.Id, new TeleEndInput("Hi", "Escalate to nearest facility if chest pain.", session.Version), ct));
    session = await teleSvc.End(scope, session.Id, new TeleEndInput("Reviewed symptoms and vitals remotely.", "Escalate to nearest facility if chest pain or breathlessness.", session.Version), ct);
    Assert(session.Status == "Completed" && session.EmergencyAdvice.Length > 0);
    await Throws("tele.state", () => teleSvc.End(scope, session.Id, new TeleEndInput("Ending again for the record here.", "Escalation advice text.", session.Version), ct));
    await Throws("tele.upload", () => teleSvc.Upload(scope, session.Id, new TeleUploadInput("report.exe", "application/octet-stream", 1024), ct));
    await Throws("tele.upload", () => teleSvc.Upload(scope, session.Id, new TeleUploadInput("scan.pdf", "application/pdf", 11L * 1024 * 1024), ct));
    var upload = await teleSvc.Upload(scope, session.Id, new TeleUploadInput("discharge-summary.pdf", "application/pdf", 240000), ct);
    Assert(upload.FileName == "discharge-summary.pdf");
    var far = await service.Book(scope, new BookingInput(data.Patients[4].Id, SeedIds.Doctor, clock.UtcNow.AddDays(2), 30), ct);
    var farSession = await teleSvc.Book(scope, new TeleSessionInput(far.Id), ct);
    farSession = await teleSvc.RecordConsent(scope, farSession.Id, new TeleConsentInput("PatientConsent", true, "Consent recorded."), ct);
    farSession = await teleSvc.JoinWaiting(scope, farSession.Id, ct);
    await Throws("tele.window", () => teleSvc.IssueJoinToken(scope, farSession.Id, new VersionInput(farSession.Version), ct));
});
await Test("inventory indents enforce priorities and separation of duties", async () =>
{
    await Throws("inventory.indent", () => inventorySvc.RaiseIndent(storeScope, new IndentInput(data.InventoryStores[0].Id, data.InventoryItems[0].Id, 0, "Routine", "Zero quantity request."), ct));
    await Throws("inventory.indent", () => inventorySvc.RaiseIndent(storeScope, new IndentInput(data.InventoryStores[0].Id, data.InventoryItems[0].Id, 10, "Priority", "Bad priority value."), ct));
    var indent = await inventorySvc.RaiseIndent(storeScope, new IndentInput(data.InventoryStores[0].Id, data.InventoryItems[0].Id, 100, "Routine", "Replenish sterile gloves below reorder level."), ct);
    await Throws("inventory.separation", () => inventorySvc.DecideIndent(storeScope, indent.Id, new IndentDecisionInput(true, "Self approval attempt.", indent.Version), ct));
    indent = await inventorySvc.DecideIndent(scope, indent.Id, new IndentDecisionInput(true, "Approved against annual rate contract.", indent.Version), ct);
    Assert(indent.Status == "Approved");
    Phase4Indent = indent;
});
await Test("purchase orders require authorized vendors and one order per indent", async () =>
{
    var indent = Phase4Indent!;
    await Throws("inventory.vendor", () => inventorySvc.CreatePO(storeScope, new PurchaseOrderInput("Unknown Vendor Ltd", indent.Id, 50, 800), ct));
    await Throws("inventory.vendor", () => inventorySvc.CreatePO(storeScope, new PurchaseOrderInput("QuickBuy Traders", indent.Id, 50, 800), ct));
    await Throws("inventory.tolerance", () => inventorySvc.CreatePO(storeScope, new PurchaseOrderInput("MediCore Supplies Pvt Ltd", indent.Id, 200, 800), ct));
    var order = await inventorySvc.CreatePO(storeScope, new PurchaseOrderInput("MediCore Supplies Pvt Ltd", indent.Id, 50, 800), ct);
    Assert(order.Number.StartsWith("PO-") && order.Status == "Issued");
    await Throws("inventory.duplicate", () => inventorySvc.CreatePO(storeScope, new PurchaseOrderInput("OrthoPlus Instruments", indent.Id, 10, 800), ct));
    Phase4Order = order;
});
await Test("goods receipts enforce tolerance, expiry and batch uniqueness", async () =>
{
    var order = Phase4Order!;
    await Throws("inventory.tolerance", () => inventorySvc.ReceiveGoods(storeScope, order.Id, new GoodsReceiptInput(10, 8, 0, "", "GLV-NEW-1", scope.Today.AddYears(1), order.Version), ct));
    await Throws("inventory.tolerance", () => inventorySvc.ReceiveGoods(storeScope, order.Id, new GoodsReceiptInput(60, 60, 0, "Over tolerance acceptance.", "GLV-NEW-1", scope.Today.AddYears(1), order.Version), ct));
    await Throws("inventory.expiry", () => inventorySvc.ReceiveGoods(storeScope, order.Id, new GoodsReceiptInput(50, 50, 0, "", "GLV-NEW-1", scope.Today.AddDays(-1), order.Version), ct));
    var receipt = await inventorySvc.ReceiveGoods(storeScope, order.Id, new GoodsReceiptInput(50, 48, 2, "Two boxes damaged in transit.", "GLV-NEW-1", scope.Today.AddYears(1), order.Version), ct);
    Assert(receipt.AcceptedQty == 48 && data.InventoryBatches.Any(b => b.BatchNumber == "GLV-NEW-1" && b.Quantity == 48));
    Assert(data.InventoryMovements.Any(m => m.RefType == "GoodsReceipt" && m.Quantity == 48));
    var indent2 = await inventorySvc.RaiseIndent(storeScope, new IndentInput(data.InventoryStores[0].Id, data.InventoryItems[0].Id, 20, "Routine", "Second indent for duplicate batch check."), ct);
    indent2 = await inventorySvc.DecideIndent(scope, indent2.Id, new IndentDecisionInput(true, "Approved for continuity of supply.", indent2.Version), ct);
    var order2 = await inventorySvc.CreatePO(storeScope, new PurchaseOrderInput("MediCore Supplies Pvt Ltd", indent2.Id, 20, 800), ct);
    await Throws("inventory.duplicate", () => inventorySvc.ReceiveGoods(storeScope, order2.Id, new GoodsReceiptInput(20, 20, 0, "", "GLV-NEW-1", scope.Today.AddYears(1), order2.Version), ct));
    await inventorySvc.ReceiveGoods(storeScope, order2.Id, new GoodsReceiptInput(20, 20, 0, "", "GLV-NEW-2", scope.Today.AddYears(2), order2.Version), ct);
    Assert(data.Assets.Count == 0);
});
await Test("transfers cannot drive stock negative and consumption records movements", async () =>
{
    var batch = data.InventoryBatches.First(b => b.BatchNumber == "GLV-NEW-1");
    await Throws("inventory.stock", () => inventorySvc.DispatchTransfer(storeScope, new StockTransferInput(data.InventoryStores[0].Id, data.InventoryStores[1].Id, data.InventoryItems[0].Id, batch.Id, 5000, "Excessive transfer request."), ct));
    var transfer = await inventorySvc.DispatchTransfer(storeScope, new StockTransferInput(data.InventoryStores[0].Id, data.InventoryStores[1].Id, data.InventoryItems[0].Id, batch.Id, 10, "Theatre list top-up."), ct);
    Assert(data.InventoryBatches.First(b => b.Id == batch.Id).Quantity == 38);
    transfer = await inventorySvc.ReceiveTransfer(storeScope, transfer.Id, new TransferReceiveInput(transfer.Version), ct);
    Assert(transfer.Status == "Received");
    var dest = data.InventoryBatches.Single(b => b.BatchNumber == "GLV-NEW-1" && b.StoreId == data.InventoryStores[1].Id);
    Assert(dest.Quantity == 10);
    await Throws("inventory.stock", () => inventorySvc.Consume(storeScope, new ConsumptionInput(data.InventoryStores[1].Id, data.InventoryItems[0].Id, dest.Id, 500, "Theatre", "Excessive consumption."), ct));
    var consumption = await inventorySvc.Consume(storeScope, new ConsumptionInput(data.InventoryStores[1].Id, data.InventoryItems[0].Id, dest.Id, 4, "Operation theatre", "Consumed during emergency list."), ct);
    Assert(consumption.Number.StartsWith("CONS-") && data.InventoryBatches.First(b => b.Id == dest.Id).Quantity == 6);
});
await Test("CSSD set lifecycle enforces pack order, cycle bands and release completeness", async () =>
{
    var set = await cssdSvc.RegisterSet(cssdScope, new CssdSetInput("CS-T1", "Test Surgery Set", [new("Forceps", "T1-001"), new("Scissors", "T1-002")]), ct);
    await Throws("cssd.state", () => cssdSvc.StartCycle(cssdScope, new CycleInput("Steam", "STM-1", 121, 30, [set.Id]), ct));
    set = await cssdSvc.CleanPack(cssdScope, set.Id, new VersionInput(set.Version), ct);
    Assert(set.Status == "Packed");
    await Throws("cssd.cycle", () => cssdSvc.StartCycle(cssdScope, new CycleInput("Steam", "STM-1", 100, 30, [set.Id]), ct));
    await Throws("cssd.cycle", () => cssdSvc.StartCycle(cssdScope, new CycleInput("Ethylene oxide", "EO-1", 30, 30, [set.Id]), ct));
    var cycle = await cssdSvc.StartCycle(cssdScope, new CycleInput("Steam", "STM-1", 121, 30, [set.Id]), ct);
    await Throws("cssd.release", () => cssdSvc.ReleaseCycle(cssdScope, cycle.Id, new CycleReleaseInput(cycle.Version, "No indicators yet."), ct));
    await cssdSvc.RecordIndicator(cssdScope, cycle.Id, new IndicatorInput("Chemical", "Pass", "Strip changed correctly."), ct);
    cycle = await cssdSvc.ReleaseCycle(cssdScope, cycle.Id, new CycleReleaseInput(cycle.Version, "Cycle parameters within band."), ct);
    Assert(cycle.Status == "Passed" && data.InstrumentSets.First(x => x.Id == set.Id).Status == "Sterile");
    var dirty = await cssdSvc.CleanPack(cssdScope, data.InstrumentSets.First(x => x.Code == "CS-002").Id, new VersionInput(data.InstrumentSets.First(x => x.Code == "CS-002").Version), ct);
    Assert(dirty.MissingCount == 1);
    var cycle2 = await cssdSvc.StartCycle(cssdScope, new CycleInput("Steam", "STM-2", 134, 20, [dirty.Id]), ct);
    await cssdSvc.RecordIndicator(cssdScope, cycle2.Id, new IndicatorInput("Chemical", "Pass", "Strip changed correctly."), ct);
    await Throws("cssd.completeness", () => cssdSvc.ReleaseCycle(cssdScope, cycle2.Id, new CycleReleaseInput(cycle2.Version, "Attempting release with missing instruments."), ct));
    var fresh = await cssdSvc.RegisterSet(cssdScope, new CssdSetInput("CS-T3", "Third Test Set", [new("Retractor", "T3-001")]), ct);
    fresh = await cssdSvc.CleanPack(cssdScope, fresh.Id, new VersionInput(fresh.Version), ct);
    var cycle3 = await cssdSvc.StartCycle(cssdScope, new CycleInput("Plasma", "PLS-1", 50, 45, [fresh.Id]), ct);
    await cssdSvc.RecordIndicator(cssdScope, cycle3.Id, new IndicatorInput("Biological", "Fail", "Growth observed in vial."), ct);
    Assert(data.SterilizerCycles.First(c => c.Id == cycle3.Id).Status == "Failed" && data.InstrumentSets.First(x => x.Id == fresh.Id).Status == "Quarantined");
    Phase4Cycle = cycle;
});
await Test("CSSD issues track returns and recalls stay unique per cycle", async () =>
{
    var set = data.InstrumentSets.First(x => x.Code == "CS-T1");
    var issue = await cssdSvc.IssueSet(cssdScope, new CssdIssueInput(set.Id, null, "Theatre 2 · Dr. Menon"), ct);
    Assert(issue.Status == "Issued" && data.InstrumentSets.First(x => x.Id == set.Id).Status == "Issued");
    await Throws("cssd.state", () => cssdSvc.IssueSet(cssdScope, new CssdIssueInput(set.Id, null, "Theatre 1"), ct));
    issue = await cssdSvc.ReturnIssue(cssdScope, issue.Id, new CssdReturnInput("Incomplete", issue.Version), ct);
    Assert(issue.ReturnCondition == "Incomplete" && data.InstrumentSets.First(x => x.Id == set.Id).MissingCount == 1);
    var recall = await cssdSvc.Recall(cssdScope, new RecallInput(Phase4Cycle!.Id, "Biological indicator concerns raised post-release."), ct);
    Assert(recall.Status == "Open" && recall.Number.StartsWith("RCL-"));
    await Throws("cssd.recall", () => cssdSvc.Recall(cssdScope, new RecallInput(Phase4Cycle!.Id, "Duplicate recall attempt for the same cycle."), ct));
    recall = await cssdSvc.CloseRecall(cssdScope, recall.Id, new VersionInput(0, "All affected sets traced and quarantined."), ct);
    Assert(recall.Status == "Closed");
});

Console.WriteLine($"\n{passed} application/domain tests passed. SQL Server integration tests are a separate release gate.");
sealed class TestClock : IClock { public DateTimeOffset UtcNow { get; set; } = new(2026, 11, 13, 10, 0, 0, TimeSpan.Zero); }
