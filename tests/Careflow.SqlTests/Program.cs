using System.Data;
using System.Data.Common;
using Careflow.Application;
using Careflow.Domain;
using Careflow.Infrastructure;

// Phase 1 + Phase 2 SQL Server integration tests. This is the database release gate:
// it applies the real T-SQL migrations and then exercises the clinical, billing, IPD,
// nursing, pharmacy and LIS flows through the ADO.NET stores against a live SQL Server.
// The suite needs CAREFLOW_TEST_SQL (or ConnectionStrings__Careflow) and skips cleanly
// when no SQL Server is configured so memory-adapter test runs stay hermetic.
var connectionString = Environment.GetEnvironmentVariable("CAREFLOW_TEST_SQL") ?? Environment.GetEnvironmentVariable("ConnectionStrings__Careflow");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.WriteLine("SKIP SQL integration tests: set CAREFLOW_TEST_SQL (or ConnectionStrings__Careflow) to a SQL Server database to run the Phase 1 + Phase 2 database release gate.");
    return;
}

var ct = CancellationToken.None;
var clock = new SystemClock();
var passwords = new IdentityPasswordService();
var database = new SqlDatabase(connectionString);
var uow = new SqlUnitOfWork(database);
var service = new HospitalService(uow, clock, passwords);
var inpatient = new InpatientService(uow, clock);
var pharmacy = new PharmacyService(uow, clock);
var laboratory = new LaboratoryService(uow, clock);
var passed = 0;

async Task Test(string name, Func<Task> run) { await run(); passed++; Console.WriteLine($"PASS {name}"); }
void Assert(bool condition, string message = "Assertion failed") { if (!condition) throw new Exception(message); }
async Task<DomainException> Throws(string code, Func<Task> run) { try { await run(); } catch (DomainException e) when (e.Code == code) { return e; } throw new Exception($"Expected domain error {code}"); }
async Task<object?> Query1(string sql, params (string Name, object Value)[] args)
{
    await using var connection = await database.OpenAsync(ct);
    await using var command = connection.CreateCommand(); command.CommandText = sql;
    foreach (var (name, value) in args) command.Parameters.Add(SqlDatabase.Parameter(command, name, value));
    return await command.ExecuteScalarAsync(ct);
}

// 1. Migration runner: applies 0001/0002 and treats the checksum ledger as immutable.
await Test("SQL migrations apply idempotently with checksum protection", async () =>
{
    var directory = Path.Combine(AppContext.BaseDirectory, "migrations");
    await database.MigrateAsync(directory, ct);
    await database.MigrateAsync(directory, ct);
    Assert(Convert.ToInt32(await Query1("SELECT COUNT(1) FROM dbo.SchemaMigrations")) >= 2, "ledger should hold the phase 1 and phase 2 migrations");
});

// 2. Fixture hospital: org/iam/catalog/ipd/lab rows created with parameterized SQL,
// exactly as a deployment would provision them (roles carry real permission grants).
var runTag = Guid.NewGuid().ToString("N")[..8];
var hospitalId = Guid.NewGuid(); var branchId = Guid.NewGuid();
var practitionerId = Guid.NewGuid();
var svcConsultId = Guid.NewGuid(); var svcProcedureId = Guid.NewGuid();
var medAId = Guid.NewGuid(); var medBId = Guid.NewGuid();
var bed1Id = Guid.NewGuid(); var bed2Id = Guid.NewGuid(); var bed3Id = Guid.NewGuid();
var testHgbId = Guid.NewGuid(); var testGluId = Guid.NewGuid();
var password = "SqlTest!2026Gate";
string UserName(string suffix) => $"sqltest_{runTag}_{suffix}";
Guid adminUserId = Guid.Empty, doctorUserId = Guid.Empty, nurseUserId = Guid.Empty, nurse2UserId = Guid.Empty;
Guid pharmacistUserId = Guid.Empty, labtechUserId = Guid.Empty, labtech2UserId = Guid.Empty, pathologistUserId = Guid.Empty;

await using (var setup = await database.OpenAsync(ct))
await using (var tx = (DbTransaction)await setup.BeginTransactionAsync(IsolationLevel.Serializable, ct))
{
    async Task<int> Exec(string sql, params (string Name, object Value)[] args)
    {
        await using var command = setup.CreateCommand(); command.Transaction = tx; command.CommandText = sql;
        foreach (var (name, value) in args) command.Parameters.Add(SqlDatabase.Parameter(command, name, value));
        return await command.ExecuteNonQueryAsync(ct);
    }
    try
    {
        await Exec("INSERT INTO org.Hospitals(Id,Code,Name) VALUES(@id,@code,@name)", ("id", hospitalId), ("code", $"SQL{runTag}"), ("name", "SQL Integration Hospital"));
        await Exec("INSERT INTO org.Branches(Id,HospitalId,Code,Name) VALUES(@id,@h,@code,@name)", ("id", branchId), ("h", hospitalId), ("code", "MAIN"), ("name", "Main Branch"));
        foreach (var key in new[] { "patients", "appointments", "consultations", "billing", "ipd", "nursing", "pharmacy", "laboratory" })
            await Exec("INSERT INTO org.ModuleInstallations(HospitalId,BranchId,ModuleKey,Enabled) VALUES(@h,@b,@key,1)", ("h", hospitalId), ("b", branchId), ("key", key));
        await Exec("INSERT INTO org.Policies(HospitalId,BranchId,[Key],Version,PayloadJson,EffectiveFromUtc,ApprovedBy,Reason) VALUES(@h,@b,'nursing.escalation',1,@json,SYSUTCDATETIME(),NULL,'Fixture thresholds await governed approval.')",
            ("h", hospitalId), ("b", branchId), ("json", "{\"spo2Below\":90,\"systolicBelow\":90,\"systolicAbove\":180,\"pulseBelow\":50,\"pulseAbove\":130,\"temperatureAtOrAbove\":39.5,\"painAtOrAbove\":8}"));

        var roles = new Dictionary<string, Guid>();
        foreach (var roleName in new[] { "Hospital Admin", "Doctor", "Nurse", "Pharmacist", "Lab Technician", "Pathologist" })
        {
            var roleId = Guid.NewGuid(); roles[roleName] = roleId;
            await Exec("INSERT INTO iam.Roles(Id,HospitalId,Name,Description) VALUES(@id,@h,@name,'SQL integration test role')", ("id", roleId), ("h", hospitalId), ("name", roleName));
            foreach (var permission in Permissions.For(roleName))
                await Exec("INSERT INTO iam.RolePermissions(HospitalId,RoleId,PermissionKey) VALUES(@h,@r,@p)", ("h", hospitalId), ("r", roleId), ("p", permission));
        }
        async Task<Guid> User(string suffix, string displayName, string roleName)
        {
            var id = Guid.NewGuid();
            await Exec("INSERT INTO iam.Users(Id,HospitalId,UserName,DisplayName,PasswordHash,Enabled) VALUES(@id,@h,@u,@name,@hash,1)", ("id", id), ("h", hospitalId), ("u", UserName(suffix)), ("name", displayName), ("hash", passwords.Hash(password)));
            var membership = Guid.NewGuid();
            await Exec("INSERT INTO iam.Memberships(Id,HospitalId,BranchId,UserId,Active) VALUES(@id,@h,@b,@u,1)", ("id", membership), ("h", hospitalId), ("b", branchId), ("u", id));
            await Exec("INSERT INTO iam.UserRoles(MembershipId,RoleId,HospitalId) VALUES(@m,@r,@h)", ("m", membership), ("r", roles[roleName]), ("h", hospitalId));
            return id;
        }
        adminUserId = await User("admin", "SQL Test Admin", "Hospital Admin");
        doctorUserId = await User("doctor", "SQL Test Doctor", "Doctor");
        nurseUserId = await User("nurse", "SQL Test Nurse", "Nurse");
        nurse2UserId = await User("nurse2", "SQL Test Nurse Two", "Nurse");
        pharmacistUserId = await User("pharmacist", "SQL Test Pharmacist", "Pharmacist");
        labtechUserId = await User("labtech", "SQL Test Labtech", "Lab Technician");
        labtech2UserId = await User("labtech2", "SQL Test Labtech Two", "Lab Technician");
        pathologistUserId = await User("pathologist", "SQL Test Pathologist", "Pathologist");

        await Exec("INSERT INTO catalog.Practitioners(Id,HospitalId,UserId,Name,Department,Initials,Color) VALUES(@id,@h,@u,'Dr SQL Fixture','Medicine','DS','#2f6fed')", ("id", practitionerId), ("h", hospitalId), ("u", doctorUserId));
        await Exec("INSERT INTO catalog.Services(Id,HospitalId,Code,Name,Department) VALUES(@id,@h,'CONS1','Consultation','Medicine')", ("id", svcConsultId), ("h", hospitalId));
        await Exec("INSERT INTO catalog.Services(Id,HospitalId,Code,Name,Department) VALUES(@id,@h,'PROC1','Minor Procedure','Surgery')", ("id", svcProcedureId), ("h", hospitalId));
        await Exec("INSERT INTO catalog.Prices(Id,HospitalId,BranchId,ServiceId,Amount,EffectiveFromUtc) VALUES(NEWID(),@h,@b,@s,500,DATEADD(day,-1,SYSUTCDATETIME()))", ("h", hospitalId), ("b", branchId), ("s", svcConsultId));
        await Exec("INSERT INTO catalog.Prices(Id,HospitalId,BranchId,ServiceId,Amount,EffectiveFromUtc) VALUES(NEWID(),@h,@b,@s,1500,DATEADD(day,-1,SYSUTCDATETIME()))", ("h", hospitalId), ("b", branchId), ("s", svcProcedureId));
        await Exec("INSERT INTO catalog.Medicines(Id,HospitalId,GenericName,BrandName,Strength,Form) VALUES(@id,@h,'Paracetamol','Paracetamol 500 mg','500 mg','Tablet')", ("id", medAId), ("h", hospitalId));
        await Exec("INSERT INTO catalog.Medicines(Id,HospitalId,GenericName,BrandName,Strength,Form) VALUES(@id,@h,'Amoxicillin','Amoxicillin 250 mg','250 mg','Capsule')", ("id", medBId), ("h", hospitalId));
        foreach (var (id, number) in new[] { (bed1Id, "T1"), (bed2Id, "T2"), (bed3Id, "T3") })
            await Exec("INSERT INTO ipd.Beds(Id,HospitalId,BranchId,Ward,Number,Kind,Status) VALUES(@id,@h,@b,'SQL-Ward',@number,'General','Available')", ("id", id), ("h", hospitalId), ("b", branchId), ("number", number));
        await Exec("INSERT INTO lab.Tests(Id,HospitalId,Code,Name,SpecimenType,Unit,ReferenceLow,ReferenceHigh,CriticalLow,CriticalHigh) VALUES(@id,@h,'HGB','Haemoglobin','Blood','g/dL',12,17,7,20)", ("id", testHgbId), ("h", hospitalId));
        await Exec("INSERT INTO lab.Tests(Id,HospitalId,Code,Name,SpecimenType,Unit,ReferenceLow,ReferenceHigh,CriticalLow,CriticalHigh) VALUES(@id,@h,'GLU','Glucose','Serum','mg/dL',70,110,40,400)", ("id", testGluId), ("h", hospitalId));
        await tx.CommitAsync(ct);
    }
    catch { await tx.RollbackAsync(ct); throw; }
}

Principal P(Guid id, string name, string role) => new(id, name, role, hospitalId, branchId);
var scope = Scope.For(P(adminUserId, "SQL Test Admin", "Hospital Admin"), clock.UtcNow);
var clinical = Scope.For(P(doctorUserId, "SQL Test Doctor", "Doctor"), clock.UtcNow);
var nurseScope = Scope.For(P(nurseUserId, "SQL Test Nurse", "Nurse"), clock.UtcNow);
var nurse2Scope = Scope.For(P(nurse2UserId, "SQL Test Nurse Two", "Nurse"), clock.UtcNow);
var pharmacyScope = Scope.For(P(pharmacistUserId, "SQL Test Pharmacist", "Pharmacist"), clock.UtcNow);
var labScope = Scope.For(P(labtechUserId, "SQL Test Labtech", "Lab Technician"), clock.UtcNow);
var lab2Scope = Scope.For(P(labtech2UserId, "SQL Test Labtech Two", "Lab Technician"), clock.UtcNow);
var pathScope = Scope.For(P(pathologistUserId, "SQL Test Pathologist", "Pathologist"), clock.UtcNow);

// Phase 1: registration, scheduling, EMR, billing, IAM and audit on the SQL stores.
var input = new PatientInput("SQL", "Patient", new DateOnly(1990, 1, 1), "Other", "+91 9000000111", $"sql-{runTag}@example.com");
Patient? registered = null;
await Test("phase 1 registration allocates a scoped UHID and audits the write", async () =>
{
    registered = await service.Register(scope, input, ct);
    Assert(registered.Uhid.StartsWith("NCH-"), "UHID should come from the SQL number sequence");
    Assert(Convert.ToInt32(await Query1("SELECT COUNT(1) FROM audit.AuditEvents WHERE HospitalId=@h AND EntityId=@id AND Action='patient.registered'", ("h", hospitalId), ("id", registered.Id))) == 1);
});
await Test("phase 1 duplicate registration demands explicit review", async () =>
{
    await Throws("patient.duplicate", () => service.Register(scope, input, ct));
    var second = await service.Register(scope, input with { DuplicateReviewReason = "Reviewed identity documents; distinct patient." }, ct);
    Assert(second.Uhid != registered!.Uhid);
});
await Test("phase 1 stale patient updates fail the Version CAS", async () =>
{
    await Throws("concurrency", () => service.UpdatePatient(scope, registered!.Id, input with { Phone = "+91 9000000112" }, registered.Version - 1, ct));
    registered = await service.UpdatePatient(scope, registered!.Id, input with { Phone = "+91 9000000112" }, registered.Version, ct);
    Assert(registered.Phone == "+91 9000000112");
});
await Test("phase 1 RBAC blocks nurse registration", async () =>
{
    await Throws("forbidden", () => service.Register(nurseScope, input with { Phone = "+91 9000000113" }, ct));
});
Appointment? booked = null; Encounter? encounter = null;
await Test("phase 1 booking conflict detection works in SQL", async () =>
{
    booked = await service.Book(scope, new BookingInput(registered!.Id, practitionerId, clock.UtcNow.AddDays(1), 30), ct);
    await Throws("booking.conflict", () => service.Book(scope, new BookingInput(registered.Id, practitionerId, clock.UtcNow.AddDays(1).AddMinutes(15)), ct));
});
await Test("phase 1 encounter signing locks content with a canonical hash", async () =>
{
    booked = await service.Transition(scope, booked!.Id, "check-in", booked.Version, "", ct);
    Assert(booked.Token.HasValue);
    encounter = await service.StartEncounter(clinical, booked.Id, ct);
    var draft = new DraftInput("SQL test complaint", "SQL test examination", "SQL test diagnosis", "SQL test plan", "Review in one week", new(37, 80, 120, 80, 70, 98), [], [new(medAId, "Client supplied name", "1 tablet", "Once daily", 2, "After food")], [], false, encounter!.Version);
    await Throws("encounter.sign", () => service.Sign(clinical, encounter!.Id, encounter.Version, ct));
    encounter = await service.SaveDraft(clinical, encounter.Id, draft with { Version = encounter.Version, AllergyReviewed = true }, ct);
    Assert(encounter!.Prescription[0].Name == "Paracetamol 500 mg");
    encounter = await service.Sign(clinical, encounter.Id, encounter.Version, ct);
    Assert(encounter.Status == "Signed" && encounter.ContentHash!.Length == 64);
    await Throws("concurrency", () => service.SaveDraft(clinical, encounter.Id, draft with { Version = encounter.Version }, ct));
});
await Test("phase 1 pricing, overpayment and payment idempotency hold on SQL", async () =>
{
    var invoice = await service.CreateInvoice(scope, new InvoiceInput(registered!.Id, [new(svcConsultId, 2)]), ct);
    Assert(invoice.Total == 1000m);
    invoice = await service.IssueInvoice(scope, invoice.Id, invoice.Version, ct);
    Assert(invoice.Status == "Issued");
    await Throws("payment.overpayment", () => service.Pay(scope, invoice.Id, new PaymentInput(invoice.Total + 1, "Cash", $"SQLPAY-{runTag}", invoice.Version), ct));
    var payment = new PaymentInput(invoice.Total, "Cash", $"SQLPAY-{runTag}", invoice.Version);
    invoice = await service.Pay(scope, invoice.Id, payment, ct);
    Assert(invoice.Status == "Paid" && invoice.Payments.Count == 1);
    var retry = await service.Pay(scope, invoice.Id, payment, ct);
    Assert(retry.Payments.Count == 1);
    await Throws("payment.idempotency", () => service.Pay(scope, invoice.Id, payment with { Amount = 1 }, ct));
});
await Test("phase 1 login, branch scoping and logout bind iam sessions", async () =>
{
    var auth = await service.Login(new LoginInput(UserName("admin"), password), hospitalId, branchId, ct, "203.0.113.10");
    Assert(await service.Authenticate(auth.Token, hospitalId, branchId, ct) is not null);
    Assert(await service.Authenticate(auth.Token, hospitalId, Guid.NewGuid(), ct) is null);
    await Throws("auth.failed", () => service.Login(new LoginInput(UserName("admin"), password), hospitalId, Guid.NewGuid(), ct));
    await Throws("auth.failed", () => service.Login(new LoginInput(UserName("admin"), "incorrect"), hospitalId, branchId, ct, "203.0.113.11"));
    Assert(Convert.ToInt32(await Query1("SELECT COUNT(1) FROM iam.LoginEvents WHERE HospitalId=@h AND Success=0 AND SourceIp='203.0.113.11'", ("h", hospitalId))) == 1);
    await service.Logout(auth.Token, hospitalId, branchId, ct);
    Assert(await service.Authenticate(auth.Token, hospitalId, branchId, ct) is null);
});

// Phase 2: policy gate, IPD/ADT, nursing, pharmacy and LIS on the SQL stores.
Patient? ipdPatient = null; Admission? admission = null;
await Test("phase 2 admissions enforce single occupancy through SQL constraints", async () =>
{
    ipdPatient = await service.Register(scope, input with { FirstName = "IPD", Phone = "+91 9000000114" }, ct);
    admission = await inpatient.Admit(scope, new AdmissionInput(ipdPatient!.Id, bed1Id, practitionerId, "Synthetic inpatient observation"), ct);
    var occupied = (await uow.ExecuteAsync(scope, s => s.FindBedAsync(bed1Id, ct), ct))!;
    Assert(occupied.Status == "Occupied" && occupied.Version > 1);
    await Throws("admission.active", () => inpatient.Admit(scope, new AdmissionInput(ipdPatient.Id, bed3Id, practitionerId, "Duplicate admission attempt"), ct));
    var other = await service.Register(scope, input with { FirstName = "Other", Phone = "+91 9000000115" }, ct);
    await Throws("bed.occupied", () => inpatient.Admit(scope, new AdmissionInput(other.Id, bed1Id, practitionerId, "Occupied bed attempt"), ct));
});
await Test("phase 2 transfer moves occupancy and cleans the vacated bed", async () =>
{
    admission = await inpatient.Transfer(scope, admission!.Id, new TransferInput(bed2Id, admission.Version, "Synthetic transfer for observation"), ct);
    Assert(admission.BedId == bed2Id);
    Assert((await uow.ExecuteAsync(scope, s => s.FindBedAsync(bed1Id, ct), ct))!.Status == "Cleaning");
    Assert((await uow.ExecuteAsync(scope, s => s.FindBedAsync(bed2Id, ct), ct))!.Status == "Occupied");
});
await Test("phase 2 nursing observations gate on an approved escalation policy", async () =>
{
    await Throws("policy.missing", () => inpatient.AddNursingNote(nurseScope, new NursingNoteInput(admission!.Id, "Assessment", "Policy gate probe", new(null, null, null, null, null, null), null, null, null, "Low"), ct));
    await Query1("UPDATE org.Policies SET ApprovedBy=@u WHERE HospitalId=@h AND [Key]='nursing.escalation'", ("u", adminUserId), ("h", hospitalId));
    var note = await inpatient.AddNursingNote(nurseScope, new NursingNoteInput(admission!.Id, "Assessment", "Synthetic deterioration review", new(39.6m, 140, 85, 60, 70, 88), 500, 400, 8, "High"), ct);
    Assert(Convert.ToInt32(await Query1("SELECT COUNT(1) FROM alerts.CriticalAlerts WHERE HospitalId=@h AND EntityId=@id AND Status='Open'", ("h", hospitalId), ("id", note.Id))) == 1);
    Assert(Convert.ToInt32(await Query1("SELECT COUNT(1) FROM audit.AuditEvents WHERE HospitalId=@h AND EntityId=@id", ("h", hospitalId), ("id", note.Id))) >= 1);
});
StockBatch? batchA1 = null;
await Test("phase 2 stock receipt validates batches and enforces CAS updates", async () =>
{
    batchA1 = await pharmacy.ReceiveStock(pharmacyScope, new ReceiveStockInput(medAId, $"SQL-A1-{runTag}", scope.Today.AddDays(30), 100, 1.5m, "SQL Supplier", $"GRN-A1-{runTag}"), ct);
    var batchA2 = await pharmacy.ReceiveStock(pharmacyScope, new ReceiveStockInput(medAId, $"SQL-A2-{runTag}", scope.Today.AddDays(90), 50, 1.5m, "SQL Supplier", $"GRN-A2-{runTag}"), ct);
    Assert(batchA2.Quantity == 50);
    await Throws("stock.duplicate", () => pharmacy.ReceiveStock(pharmacyScope, new ReceiveStockInput(medAId, batchA1!.BatchNumber, scope.Today.AddDays(30), 5, 1.5m, "SQL Supplier", $"GRN-A3-{runTag}"), ct));
    await Throws("stock.fields", () => pharmacy.ReceiveStock(pharmacyScope, new ReceiveStockInput(medAId, $"SQL-EXP-{runTag}", scope.Today.AddDays(-1), 5, 1, "SQL Supplier", $"GRN-EXP-{runTag}"), ct));
    var stale = (await uow.ExecuteAsync(pharmacyScope, s => s.FindStockBatchAsync(batchA1!.Id, ct), ct))!;
    Assert(stale.Quantity == 100);
    await Throws("concurrency", () => uow.ExecuteAsync<bool>(pharmacyScope, async s => { await s.UpdateStockBatchAsync(stale with { Quantity = stale.Quantity - 1, Version = stale.Version + 1 }, 55m, ct); return true; }, ct));
    Assert(Convert.ToInt32(await Query1("SELECT COUNT(1) FROM pharmacy.StockMovements WHERE HospitalId=@h AND Kind='Receipt'", ("h", hospitalId))) == 2);
});
await Test("phase 2 eMAR decrements the named batch and rejects duplicate times", async () =>
{
    var medOrder = await inpatient.OrderMedication(clinical, new MedicationOrderInput(admission!.Id, medAId, "1 tablet", "Oral", "Twice daily", clock.UtcNow.AddMinutes(-1), clock.UtcNow.AddDays(1)), ct);
    var givenAt = clock.UtcNow;
    await inpatient.Administer(nurseScope, new AdministerInput(medOrder.Id, batchA1!.Id, 1, givenAt, "", "Synthetic administration"), ct);
    Assert((await uow.ExecuteAsync(scope, s => s.FindStockBatchAsync(batchA1!.Id, ct), ct))!.Quantity == 99);
    await Throws("medication.duplicate", () => inpatient.Administer(nurseScope, new AdministerInput(medOrder.Id, batchA1!.Id, 1, givenAt, "", "Duplicate attempt"), ct));
});
await Test("phase 2 shift handovers route to nursing staff and gate acknowledgement", async () =>
{
    var handover = await inpatient.CreateHandover(nurseScope, new HandoverInput(nurse2UserId, "Synthetic handover summary for admitted patients.", [admission!.PatientId]), ct);
    await Throws("handover.state", () => inpatient.AcknowledgeHandover(nurseScope, handover.Id, new VersionInput(handover.Version), ct));
    var ack = await inpatient.AcknowledgeHandover(nurse2Scope, handover.Id, new VersionInput(handover.Version), ct);
    Assert(ack.Status == "Acknowledged");
});
await Test("phase 2 OPD dispensing is FEFO and idempotent on SQL", async () =>
{
    var request = new DispenseInput("OPD", registered!.Id, encounter!.Id, null, [new DispenseLineInput(medAId, 5)], $"SQL-DISP-{runTag}");
    var dispense = await pharmacy.Dispense(pharmacyScope, request, ct);
    Assert((await uow.ExecuteAsync(scope, s => s.FindStockBatchAsync(batchA1!.Id, ct), ct))!.Quantity == 94, "FEFO should draw from the earliest expiry batch");
    var retry = await pharmacy.Dispense(pharmacyScope, request, ct);
    Assert(retry.Id == dispense.Id);
    await Throws("dispense.idempotency", () => pharmacy.Dispense(pharmacyScope, request with { Lines = [new DispenseLineInput(medAId, 2)] }, ct));
    await Throws("dispense.prescription", () => pharmacy.Dispense(pharmacyScope, new DispenseInput("OPD", registered.Id, encounter.Id, null, [new DispenseLineInput(medBId, 1)], $"SQL-DISP-2-{runTag}"), ct));
});
await Test("phase 2 IPD dispensing requires an active medication order", async () =>
{
    await Throws("dispense.order", () => pharmacy.Dispense(pharmacyScope, new DispenseInput("IPD", ipdPatient!.Id, null, admission!.Id, [new DispenseLineInput(medBId, 1)], $"SQL-IPD-{runTag}"), ct));
});
LabOrder? labOrder = null;
await Test("phase 2 LIS orders allocate barcode and accession on SQL", async () =>
{
    labOrder = await laboratory.Order(clinical, new LabOrderInput(ipdPatient!.Id, testHgbId, null, null, "STAT", "Synthetic critical value workflow"), ct);
    labOrder = await laboratory.Collect(labScope, labOrder.Id, new CollectSpecimenInput("Blood", 2, labOrder.Version), ct);
    Assert(labOrder.Barcode != null && labOrder.Accession != null && labOrder.Status == "Collected");
    labOrder = await laboratory.Process(labScope, labOrder.Id, new VersionInput(labOrder.Version), ct);
});
await Test("phase 2 LIS role separation, critical alerting and immutable reports", async () =>
{
    var result = await laboratory.EnterResult(labScope, labOrder!.Id, new LabResultInput("6.5", "g/dL", null, null, labOrder.Version), ct);
    Assert(result.Flag == "Critical low");
    Assert(Convert.ToInt32(await Query1("SELECT COUNT(1) FROM alerts.CriticalAlerts WHERE HospitalId=@h AND EntityId=@id AND Kind='Laboratory'", ("h", hospitalId), ("id", labOrder.Id))) == 1);
    labOrder = (await uow.ExecuteAsync(scope, s => s.FindLabOrderAsync(labOrder!.Id, ct), ct))!;
    await Throws("lab.separation", () => laboratory.Validate(labScope, labOrder!.Id, new VersionInput(labOrder.Version), ct));
    await laboratory.Validate(lab2Scope, labOrder.Id, new VersionInput(labOrder.Version), ct);
    labOrder = (await uow.ExecuteAsync(scope, s => s.FindLabOrderAsync(labOrder!.Id, ct), ct))!;
    var authorized = await laboratory.Authorize(pathScope, labOrder.Id, new VersionInput(labOrder.Version), ct);
    Assert(authorized.ContentHash!.Length == 64);
    Assert((string)(await Query1("SELECT Status FROM lab.Orders WHERE HospitalId=@h AND Id=@id", ("h", hospitalId), ("id", labOrder.Id)))! == "Authorized");
    await Throws("lab.state", () => laboratory.EnterResult(labScope, labOrder!.Id, new LabResultInput("7.1", "g/dL", null, null, labOrder.Version), ct));
});
await Test("phase 2 critical alerts acknowledge once and then lock", async () =>
{
    var alertId = (Guid)(await Query1("SELECT TOP(1) Id FROM alerts.CriticalAlerts WHERE HospitalId=@h AND EntityId=@id AND Kind='Laboratory'", ("h", hospitalId), ("id", labOrder!.Id)))!;
    var ack = await laboratory.AcknowledgeAlert(clinical, alertId, new AlertAcknowledgeInput("Synthetic clinician acknowledgement and escalation."), ct);
    Assert(ack.Status == "Acknowledged");
    await Throws("alert.state", () => laboratory.AcknowledgeAlert(clinical, alertId, new AlertAcknowledgeInput("Duplicate acknowledgement."), ct));
});
await Test("phase 2 specimen rejection preserves reason and blocks processing", async () =>
{
    var order = await laboratory.Order(clinical, new LabOrderInput(ipdPatient!.Id, testGluId, null, null, "Routine", ""), ct);
    order = await laboratory.Collect(labScope, order.Id, new CollectSpecimenInput("Serum", 2, order.Version), ct);
    order = await laboratory.Reject(labScope, order.Id, new VersionInput(order.Version, "Hemolysed synthetic specimen"), ct);
    Assert(order.Status == "Rejected");
    await Throws("lab.state", () => laboratory.Process(labScope, order.Id, new VersionInput(order.Version), ct));
});
await Test("phase 2 discharge requires a summary and releases the bed", async () =>
{
    await Throws("forbidden", () => inpatient.Discharge(scope, admission!.Id, new DischargeInput(admission.Version, "Synthetic discharge summary sufficient for review.", "Synthetic diagnosis", "Synthetic follow-up instructions"), ct));
    await Throws("care.fields", () => inpatient.Discharge(clinical, admission!.Id, new DischargeInput(admission.Version, "Too short", "Synthetic diagnosis", "Synthetic follow-up instructions"), ct));
    admission = await inpatient.Discharge(clinical, admission!.Id, new DischargeInput(admission.Version, "Synthetic discharge summary sufficient for clinical review.", "Synthetic diagnosis", "Synthetic follow-up instructions"), ct);
    Assert(admission.Status == "Discharged");
    Assert((await uow.ExecuteAsync(scope, s => s.FindBedAsync(admission!.BedId, ct), ct))!.Status == "Cleaning");
    await Throws("admission.state", () => inpatient.Transfer(scope, admission.Id, new TransferInput(bed3Id, admission.Version, "Invalid post-discharge transfer"), ct));
    Assert(Convert.ToInt32(await Query1("SELECT COUNT(1) FROM ipd.BedEvents WHERE HospitalId=@h AND Action='Discharged'", ("h", hospitalId))) == 1);
});

Console.WriteLine($"\n{passed} SQL integration tests passed against SQL Server.");
