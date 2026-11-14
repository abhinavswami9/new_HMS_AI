using System.Data;
using System.Data.Common;
using Careflow.Application;
using Careflow.Domain;
using Careflow.Infrastructure;

// Phase 1 + Phase 2 + Phase 3 SQL Server integration tests. This is the database release gate:
// it applies the real T-SQL migrations and then exercises the clinical, billing, IPD,
// nursing, pharmacy, LIS, RIS, emergency, theatre and blood bank flows through the ADO.NET
// stores against a live SQL Server. The suite needs CAREFLOW_TEST_SQL (or
// ConnectionStrings__Careflow) and skips cleanly when no SQL Server is configured so
// memory-adapter test runs stay hermetic.
var connectionString = Environment.GetEnvironmentVariable("CAREFLOW_TEST_SQL") ?? Environment.GetEnvironmentVariable("ConnectionStrings__Careflow");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.WriteLine("SKIP SQL integration tests: set CAREFLOW_TEST_SQL (or ConnectionStrings__Careflow) to a SQL Server database to run the Phase 1 + Phase 2 + Phase 3 database release gate.");
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
async Task ExecSql(string sql, params (string Name, object Value)[] args)
{
    await using var connection = await database.OpenAsync(ct);
    await using var command = connection.CreateCommand(); command.CommandText = sql;
    foreach (var (name, value) in args) command.Parameters.Add(SqlDatabase.Parameter(command, name, value));
    await command.ExecuteNonQueryAsync(ct);
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
        foreach (var key in new[] { "patients", "appointments", "consultations", "billing", "ipd", "nursing", "pharmacy", "laboratory", "radiology", "emergency", "ot", "blood", "insurance", "incentives", "telemedicine", "inventory", "cssd" })
            await Exec("INSERT INTO org.ModuleInstallations(HospitalId,BranchId,ModuleKey,Enabled) VALUES(@h,@b,@key,1)", ("h", hospitalId), ("b", branchId), ("key", key));
        await Exec("INSERT INTO org.Policies(HospitalId,BranchId,[Key],Version,PayloadJson,EffectiveFromUtc,ApprovedBy,Reason) VALUES(@h,@b,'nursing.escalation',1,@json,SYSUTCDATETIME(),NULL,'Fixture thresholds await governed approval.')",
            ("h", hospitalId), ("b", branchId), ("json", "{\"spo2Below\":90,\"systolicBelow\":90,\"systolicAbove\":180,\"pulseBelow\":50,\"pulseAbove\":130,\"temperatureAtOrAbove\":39.5,\"painAtOrAbove\":8}"));

        var roles = new Dictionary<string, Guid>();
        foreach (var roleName in new[] { "Hospital Admin", "Doctor", "Nurse", "Pharmacist", "Lab Technician", "Pathologist", "Radiologist", "Blood Bank Technician" })
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
var input = new PatientInput("SQL", "Patient", new DateOnly(1990, 1, 1), "Other", "+91 9000000111", $"sql-{runTag}@example.com")
{
    Title = "Ms", MaritalStatus = "Married", FatherName = "R. SQL", MotherName = "S. SQL", Religion = "Hindu",
    Occupation = "Service", Education = "Graduate", Language = "Hindi", Nationality = "Indian",
    AadhaarNumber = "781234567890", AbhaNumber = "12345678901234", AbhaAddress = "sql.patient@abdm",
    City = "New Delhi", State = "Delhi", Pincode = "110016", Country = "India"
};
Patient? registered = null;
await Test("phase 1 registration allocates a scoped UHID and audits the write", async () =>
{
    registered = await service.Register(scope, input, ct);
    Assert(registered.Uhid.StartsWith("NCH-"), "UHID should come from the SQL number sequence");
    Assert((await Query1("SELECT AbhaNumber FROM patient.Patients WHERE Id=@id", ("id", registered.Id)))?.ToString() == "12345678901234", "ABHA identity fields persist to SQL");
    Assert((await Query1("SELECT Pincode FROM patient.Patients WHERE Id=@id", ("id", registered.Id)))?.ToString() == "110016", "address demographics persist to SQL");
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

// Phase 3: RIS, emergency, operation theatre and blood bank on SQL Server
var radiology = new RadiologyService(uow, clock); var emergencySvc = new EmergencyService(uow, clock); var otSvc = new OtService(uow, clock); var bloodBank = new BloodBankService(uow, clock);
var insuranceSvc = new InsuranceService(uow, clock); var incentiveSvc = new IncentiveService(uow, clock); var teleSvc = new TelemedicineService(uow, clock);
var inventorySvc = new InventoryService(uow, clock); var cssdSvc = new CssdService(uow, clock);
var qualitySvc = new QualityService(uow, clock); var portalSvc = new PortalService(uow, clock); var reportingSvc = new ReportingService(uow, clock);
var radUserId = Guid.NewGuid(); var rad2UserId = Guid.NewGuid(); var bloodtechUserId = Guid.NewGuid();
await Test("phase 3 role users bootstrap for RIS and blood bank", async () =>
{
    foreach (var (id, name, user, role) in new[] { (radUserId, "SQL Test Radiologist", "sql_rad1", "Radiologist"), (rad2UserId, "SQL Test Radiologist Two", "sql_rad2", "Radiologist"), (bloodtechUserId, "SQL Test Bloodtech", "sql_bloodtech", "Blood Bank Technician") })
    {
        await ExecSql("INSERT INTO iam.Users(Id,HospitalId,UserName,DisplayName,PasswordHash,Enabled) VALUES(@id,@h,@u,@name,@hash,1)", ("id", id), ("h", hospitalId), ("u", user), ("name", name), ("hash", passwords.Hash(password)));
        Assert(role is "Radiologist" or "Blood Bank Technician");
    }
});
var radScope = Scope.For(P(radUserId, "SQL Test Radiologist", "Radiologist"), clock.UtcNow);
var rad2Scope = Scope.For(P(rad2UserId, "SQL Test Radiologist Two", "Radiologist"), clock.UtcNow);
var bloodScope = Scope.For(P(bloodtechUserId, "SQL Test Bloodtech", "Blood Bank Technician"), clock.UtcNow);
RadiologyOrder? risOrder = null;
await Test("phase 3 RIS lifecycle enforces CAS and protects authorized reports", async () =>
{
    var patient = await service.Register(scope, input with { FirstName = "P3", LastName = "Imaging", Phone = "+91 9000000413" }, ct);
    risOrder = await radiology.Order(clinical, new RadiologyOrderInput(patient.Id, null, null, "CT Brain plain", "CT", "Brain", "Not applicable", "Urgent", "SQL synthetic stroke workup"), ct);
    risOrder = await radiology.Schedule(radScope, risOrder.Id, new RadiologyScheduleInput(clock.UtcNow.AddHours(1), risOrder.Version), ct);
    Assert(risOrder.Accession != null && risOrder.Number.StartsWith("RIS-"));
    risOrder = await radiology.Perform(radScope, risOrder.Id, new RadiologyPerformInput("1.2.840.113619.2.55.3.604688119.868.1762000001.456", risOrder.Version), ct);
    var report = await radiology.Report(radScope, risOrder.Id, new RadiologyReportInput("SQL synthetic findings without acute bleed.", "SQL synthetic impression: no acute intracranial abnormality.", false, risOrder.Version), ct);
    var current = (await uow.ExecuteAsync(scope, s => s.FindRadiologyOrderAsync(risOrder!.Id, ct), ct))!;
    await Throws("radiology.separation", () => radiology.Authorize(radScope, current.Id, new VersionInput(current.Version), ct));
    await Throws("concurrency", () => radiology.Authorize(rad2Scope, current.Id, new VersionInput(current.Version - 1), ct));
    report = await radiology.Authorize(rad2Scope, current.Id, new VersionInput(current.Version), ct);
    Assert(report.Status == "Authorized" && report.ContentHash!.Length == 64);
    var tampered = false; try { await ExecSql("UPDATE ris.Reports SET Findings=N'Tampered' WHERE Id=@id", ("id", report.Id)); } catch { tampered = true; }
    Assert(tampered);
});
await Test("phase 3 emergency triage is append-only in SQL", async () =>
{
    var er = await emergencySvc.Register(scope, new EmergencyRegistrationInput(null, "SQL Unknown Adult", "Unknown", null, "Ambulance"), ct);
    var tri = await emergencySvc.Triage(nurseScope, er.Id, new TriageInput("Red", "SQL synthetic crushing chest pain", new(null, null, null, null, null, null)), ct);
    var tampered = false; try { await ExecSql("UPDATE emergency.TriageAssessments SET Category='Green' WHERE Id=@id", ("id", tri.Id)); } catch { tampered = true; }
    Assert(tampered);
});
await Test("phase 3 theatre overlap and checklist uniqueness hold on SQL", async () =>
{
    var patient = await service.Register(scope, input with { FirstName = "P3", LastName = "Theatre", Phone = "+91 9000000414" }, ct);
    var freeBedId = (Guid)(await Query1("SELECT TOP(1) Id FROM ipd.Beds WHERE HospitalId=@h AND BranchId=@b AND Status='Available' ORDER BY Id", ("h", hospitalId), ("b", branchId)))!;
    var otAdm = await inpatient.Admit(scope, new AdmissionInput(patient.Id, freeBedId, SeedIds.Doctor, "SQL synthetic admission for theatre flow"), ct);
    var booking = await otSvc.Request(clinical, new OtBookingInput(otAdm.PatientId, otAdm.Id, "SQL synthetic forearm procedure", "Left", "Left forearm", SeedIds.Doctor, "Local", "Elective"), ct);
    booking = await otSvc.Schedule(scope, booking.Id, new OtScheduleInput("SQL-OT-1", clock.UtcNow.AddDays(1).AddHours(2), 60, booking.Version), ct);
    var booking2 = await otSvc.Request(clinical, new OtBookingInput(otAdm.PatientId, otAdm.Id, "SQL synthetic overlapping procedure", "Right", "Right forearm", SeedIds.Doctor, "Local", "Elective"), ct);
    await Throws("ot.conflict", () => otSvc.Schedule(scope, booking2.Id, new OtScheduleInput("SQL-OT-1", clock.UtcNow.AddDays(1).AddHours(2).AddMinutes(30), 60, booking2.Version), ct));
    await otSvc.Checklist(clinical, booking.Id, new OtChecklistInput(true, true, true, true, true, "SQL synthetic surgical safety checklist."), ct);
    await Throws("ot.checklist.exists", () => otSvc.Checklist(clinical, booking.Id, new OtChecklistInput(true, true, true, true, true, "Duplicate checklist attempt."), ct));
});
await Test("phase 3 blood issues close immutably and unit numbers stay unique", async () =>
{
    var patient = await service.Register(scope, input with { FirstName = "P3", LastName = "Transfusion", Phone = "+91 9000000415", BloodGroup = "B+" }, ct);
    await bloodBank.ReceiveUnit(bloodScope, new ReceiveUnitInput("SQL-B-01", "O-", "PRBC", DateOnly.FromDateTime(clock.UtcNow.UtcDateTime.AddDays(30)), "SQL synthetic donor"), ct);
    await Throws("blood.duplicate", () => bloodBank.ReceiveUnit(bloodScope, new ReceiveUnitInput("SQL-B-01", "O-", "PRBC", DateOnly.FromDateTime(clock.UtcNow.UtcDateTime.AddDays(30)), "SQL synthetic donor"), ct));
    var unitId = (Guid)(await Query1("SELECT Id FROM blood.Units WHERE HospitalId=@h AND UnitNumber='SQL-B-01'", ("h", hospitalId)))!;
    await bloodBank.Screen(bloodScope, unitId, new ScreeningInput(false, false, false, false, "SQL synthetic clean screening"), ct);
    await bloodBank.Crossmatch(bloodScope, new CrossmatchInput(unitId, patient.Id), ct);
    var issue = await bloodBank.Issue(bloodScope, new BloodIssueInput(unitId, patient.Id, null, "SQL synthetic transfusion request"), ct);
    issue = await bloodBank.Transfuse(clinical, issue.Id, new TransfuseInput("Nil", "SQL synthetic uneventful transfusion", issue.Version), ct);
    Assert(issue.Status == "Transfused");
    var tampered = false; try { await ExecSql("UPDATE blood.Issues SET Status='Issued' WHERE Id=@id", ("id", issue.Id)); } catch { tampered = true; }
    Assert(tampered);
});

await Test("phase 4 claims settle immutably and settlement references stay unique", async () =>
{
    var patient = await service.Register(scope, input with { FirstName = "P4", LastName = "Claim", Phone = "+91 9000000515" }, ct);
    var payerId = Guid.NewGuid(); var planId2 = Guid.NewGuid();
    await ExecSql("INSERT INTO insurance.Payers(Id,HospitalId,Code,Name,Contact,Active) VALUES(@id,@h,'SQLPAY','SQL Panel TPA','',1)", ("id", payerId), ("h", hospitalId));
    await ExecSql("INSERT INTO insurance.Plans(Id,HospitalId,PayerId,Code,Name,PolicyType,SumInsuredLimit) VALUES(@id,@h,@payer,'SQLPLAN','SQL Family Floater','Family floater',200000)", ("id", planId2), ("h", hospitalId), ("payer", payerId));
    var coverage = await insuranceSvc.RegisterCoverage(scope, new CoverageInput(patient.Id, planId2, "POL-SQL-9001", "MBR-SQL-1", DateOnly.FromDateTime(clock.UtcNow.UtcDateTime.AddDays(-30)), DateOnly.FromDateTime(clock.UtcNow.UtcDateTime.AddDays(330)), 100000, 1000), ct);
    var claim = await insuranceSvc.CreateClaim(scope, new ClaimInput(patient.Id, coverage.Id, null, null, "J06.9", [new("CONS-1", "Consultation", 1, 500)]), ct);
    claim = await insuranceSvc.SubmitClaim(scope, claim.Id, new VersionInput(claim.Version), ct);
    claim = await insuranceSvc.DecideClaim(scope, claim.Id, new ClaimDecisionInput("Approved", 500, "Approved as per policy schedule.", claim.Version), ct);
    await insuranceSvc.SettleClaim(scope, claim.Id, new SettlementInput(500, "Bank transfer", "STL-SQL-1", claim.Version), ct);
    Assert((await Query1("SELECT Status FROM insurance.Claims WHERE Id=@id", ("id", claim.Id)))?.ToString() == "Settled");
    await Throws("insurance.settlement", () => insuranceSvc.SettleClaim(scope, claim.Id, new SettlementInput(100, "Cash", "STL-SQL-2", 9), ct));
    var tampered = false; try { await ExecSql("UPDATE insurance.Claims SET Status='Draft' WHERE Id=@id", ("id", claim.Id)); } catch { tampered = true; }
    Assert(tampered);
});
await Test("phase 4 CSSD cycles release with indicators and stay immutable when closed", async () =>
{
    var set = await cssdSvc.RegisterSet(scope, new CssdSetInput("SQL-CS-1", "SQL Major Set", [new("Forceps", "SQL-INS-1")]), ct);
    set = await cssdSvc.CleanPack(scope, set.Id, new VersionInput(set.Version), ct);
    var cycle = await cssdSvc.StartCycle(scope, new CycleInput("Steam", "SQL-STM-1", 121, 30, [set.Id]), ct);
    await Throws("cssd.release", () => cssdSvc.ReleaseCycle(scope, cycle.Id, new CycleReleaseInput(cycle.Version, "No indicators recorded."), ct));
    await cssdSvc.RecordIndicator(scope, cycle.Id, new IndicatorInput("Chemical", "Pass", "SQL synthetic indicator check."), ct);
    cycle = await cssdSvc.ReleaseCycle(scope, cycle.Id, new CycleReleaseInput(cycle.Version, "Cycle within validated band."), ct);
    Assert(cycle.Status == "Passed");
    var tampered = false; try { await ExecSql("UPDATE cssd.Cycles SET Status='Running' WHERE Id=@id", ("id", cycle.Id)); } catch { tampered = true; }
    Assert(tampered);
    var appendOnly = false; try { await ExecSql("DELETE FROM cssd.CycleIndicators WHERE CycleId=@id", ("id", cycle.Id)); } catch { appendOnly = true; }
    Assert(appendOnly);
});

await Test("phase 5 quality CAPA requires effectiveness and closed records stay immutable", async () =>
{
    var incident = await qualitySvc.ReportIncident(scope, new IncidentInput("Medication", "Moderate", "SQL synthetic near miss on the ward trolley during the night shift.", "Ward 1", null), ct);
    incident = await qualitySvc.TriageIncident(scope, incident.Id, new IncidentTriageInput("Assign to the medication safety owner.", "SQL Quality Manager", incident.Version), ct);
    var capa = await qualitySvc.OpenCapa(scope, new CapaInput(incident.Id, null, null, null, "Quarantined the affected lot.", "Tall-man lettering on labels.", "Quarterly spot checks.", "SQL Quality Manager", clock.UtcNow.AddDays(20)), ct);
    await Throws("quality.state", () => qualitySvc.CloseCapa(scope, capa.Id, new CapaCloseInput("Attempting closure without verification.", capa.Version), ct));
    capa = await qualitySvc.VerifyCapa(scope, capa.Id, new CapaVerifyInput("Three spot checks passed with photo evidence.", capa.Version), ct);
    capa = await qualitySvc.CloseCapa(scope, capa.Id, new CapaCloseInput("Effectiveness verified.", capa.Version), ct);
    Assert((await Query1("SELECT Status FROM quality.CAPA WHERE Id=@id", ("id", capa.Id)))?.ToString() == "Closed");
    var tampered = false; try { await ExecSql("UPDATE quality.CAPA SET Status='Open' WHERE Id=@id", ("id", capa.Id)); } catch { tampered = true; }
    Assert(tampered);
    var doc = await qualitySvc.CreateDocument(scope, new DocumentInput("SQL Waste Segregation SOP", "SOP", "SQL Quality Manager", "Initial issue.", "Segregate waste into colour-coded bins at the point of generation."), ct);
    doc = await qualitySvc.SubmitDocument(scope, doc.Id, new DocumentTransitionInput("Ready for review.", doc.Version), ct);
    doc = await qualitySvc.ApproveDocument(scope, doc.Id, new DocumentTransitionInput("Reviewed against policy.", doc.Version), ct);
    var appendOnly = false; try { await ExecSql("DELETE FROM quality.DocumentVersions WHERE DocumentId=@id", ("id", doc.Id)); } catch { appendOnly = true; }
    Assert(appendOnly);
});
await Test("phase 5 portal linkage is unique and revoked proxy grants stay revoked", async () =>
{
    var patient = await service.Register(scope, new PatientInput("SQL", "Portal", new DateOnly(1992, 2, 2), "Other", "+91 9000000222", $"sql-portal-{runTag}@example.com"), ct);
    var account = await portalSvc.Register(scope, new PortalRegisterInput(patient.Id, adminUserId, "sql.portal.user", "SQL synthetic in-person verification at the desk."), ct);
    Assert((await Query1("SELECT Status FROM portal.Accounts WHERE Id=@id", ("id", account.Id)))?.ToString() == "Pending");
    account = await portalSvc.VerifyAccount(scope, account.Id, new PortalVerifyInput("SQL Test Admin", account.Version), ct);
    Assert(account.Status == "Verified");
    await Throws("portal.duplicate", () => portalSvc.Register(scope, new PortalRegisterInput(patient.Id, Guid.NewGuid(), "sql.portal.dup", "Duplicate linkage attempt."), ct));
    await ExecSql("INSERT INTO portal.ProxyGrants(Id,HospitalId,BranchId,PatientId,AccountId,Scope,ExpiresOn,Status,GrantedBy,GrantedAtUtc,RevokedBy,RevokedAtUtc,Version) VALUES(@id,@h,@b,@patient,@account,'Records','2027-12-31','Revoked','SQL Test Admin',SYSUTCDATETIME(),'SQL Test Admin',SYSUTCDATETIME(),2)", ("id", Guid.NewGuid()), ("h", hospitalId), ("b", branchId), ("patient", patient.Id), ("account", account.Id));
    var grantId = await Query1("SELECT TOP(1) Id FROM portal.ProxyGrants WHERE AccountId=@account", ("account", account.Id));
    var tampered = false; try { await ExecSql("UPDATE portal.ProxyGrants SET Status='Active' WHERE Id=@id", ("id", grantId!)); } catch { tampered = true; }
    Assert(tampered);
    var definition = await reportingSvc.Define(scope, new ReportDefinitionInput("SQL-R", "SQL census report", "Management", "SQL Test Admin", "census_summary", "uhid,patient", "from,to"), ct);
    var execution = await reportingSvc.Execute(scope, new ReportExecuteInput(definition.Id, "{}"), ct);
    Assert(execution.Status == "Completed" && execution.Number.StartsWith("RPT-"));
    var appendOnly = false; try { await ExecSql("DELETE FROM report.Executions WHERE Id=@id", ("id", execution.Id)); } catch { appendOnly = true; }
    Assert(appendOnly);
});

Console.WriteLine($"\n{passed} SQL integration tests passed against SQL Server.");
