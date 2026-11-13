using System.Data;
using System.Data.Common;
using System.Text.Json;
using Careflow.Application;
using Careflow.Domain;
namespace Careflow.Infrastructure;

public sealed partial class SqlHospitalStore(DbConnection connection, DbTransaction transaction, Scope scope) : IHospitalStore
{
    private DbCommand Command(string sql, params (string Name, object? Value)[] args)
    {
        var c = connection.CreateCommand(); c.Transaction = transaction; c.CommandText = sql; c.CommandTimeout = 30;
        foreach (var (name, value) in args) c.Parameters.Add(SqlDatabase.Parameter(c, name, value));
        return c;
    }
    private async Task<List<T>> Query<T>(string sql, Func<DbDataReader, T> map, params (string, object?)[] args)
    {
        await using var c = Command(sql, args); await using var r = await c.ExecuteReaderAsync(); var result = new List<T>();
        while (await r.ReadAsync()) result.Add(map(r)); return result;
    }
    private async Task<int> Execute(string sql, params (string, object?)[] args) { await using var c = Command(sql, args); return await c.ExecuteNonQueryAsync(); }
    private static string S(DbDataReader r, string name) => r[name] is DBNull ? "" : Convert.ToString(r[name])!;
    private static Guid G(DbDataReader r, string name) => r.GetGuid(r.GetOrdinal(name));
    private static Guid? GN(DbDataReader r, string name) => r[name] is DBNull ? null : r.GetGuid(r.GetOrdinal(name));
    private static int? IN(DbDataReader r, string name) => r[name] is DBNull ? null : Convert.ToInt32(r[name]);
    private static int I(DbDataReader r, string name) => Convert.ToInt32(r[name]);
    private static long L(DbDataReader r, string name) => Convert.ToInt64(r[name]);
    private static decimal D(DbDataReader r, string name) => Convert.ToDecimal(r[name]);
    private static decimal? DN(DbDataReader r, string name) => r[name] is DBNull ? null : Convert.ToDecimal(r[name]);
    private static bool B(DbDataReader r, string name) => Convert.ToBoolean(r[name]);
    private static DateTimeOffset T(DbDataReader r, string name) => new(DateTime.SpecifyKind(Convert.ToDateTime(r[name]), DateTimeKind.Utc), TimeSpan.Zero);
    private static DateTimeOffset? TN(DbDataReader r, string name) => r[name] is DBNull ? null : T(r, name);
    private static DateOnly Date(DbDataReader r, string name) => DateOnly.FromDateTime(Convert.ToDateTime(r[name]));
    private (string, object?) H => ("h", scope.HospitalId);
    private (string, object?) Branch => ("b", scope.BranchId);
    private static DomainException Conflict() => new("concurrency", "The record changed or is no longer mutable. Refresh and retry.", 409);
    private const string PatientColumns = "Id,Uhid,FirstName,LastName,DateOfBirth,Sex,Phone,Email,BloodGroup,Address,EmergencyContact,CreatedAtUtc,Active,Version";
    private static Patient MapPatient(DbDataReader r) => new(G(r, "Id"), S(r, "Uhid"), S(r, "FirstName"), S(r, "LastName"), Date(r, "DateOfBirth"), S(r, "Sex"), S(r, "Phone"), S(r, "Email"), S(r, "BloodGroup"), S(r, "Address"), S(r, "EmergencyContact"), T(r, "CreatedAtUtc"), B(r, "Active"), L(r, "Version"));
    private const string AppointmentColumns = "Id,PatientId,DoctorId,StartsAtUtc,DurationMinutes,Mode,Reason,Status,Token,CreatedAtUtc,Version";
    private static Appointment MapAppointment(DbDataReader r) => new(G(r, "Id"), G(r, "PatientId"), G(r, "DoctorId"), T(r, "StartsAtUtc"), I(r, "DurationMinutes"), S(r, "Mode"), S(r, "Reason"), S(r, "Status"), IN(r, "Token"), T(r, "CreatedAtUtc"), L(r, "Version"));
    private const string EncounterColumns = "Id,PatientId,AppointmentId,DoctorId,Status,ChiefComplaint,Examination,Diagnosis,[Plan],FollowUp,TemperatureC,Pulse,Systolic,Diastolic,WeightKg,Spo2,AllergyReviewed,SignedBy,SignedAtUtc,ContentHash,UpdatedAtUtc,Version";
    private static Encounter MapEncounter(DbDataReader r) => new(G(r, "Id"), G(r, "PatientId"), GN(r, "AppointmentId"), G(r, "DoctorId"), S(r, "Status"), S(r, "ChiefComplaint"), S(r, "Examination"), S(r, "Diagnosis"), S(r, "Plan"), S(r, "FollowUp"),
        new(DN(r, "TemperatureC"), IN(r, "Pulse"), IN(r, "Systolic"), IN(r, "Diastolic"), DN(r, "WeightKg"), IN(r, "Spo2")), [], [], [], T(r, "UpdatedAtUtc"), S(r, "SignedBy") is "" ? null : S(r, "SignedBy"), TN(r, "SignedAtUtc"), S(r, "ContentHash") is "" ? null : S(r, "ContentHash"), L(r, "Version"))
    { AllergyReviewed = B(r, "AllergyReviewed") };
    public async Task<IReadOnlyList<Doctor>> LoadDoctorsAsync(CancellationToken ct) => await Query("SELECT Id,Name,Department,Initials,Color FROM catalog.Practitioners WHERE HospitalId=@h AND Active=1 ORDER BY Name", r => new Doctor(G(r, "Id"), S(r, "Name"), S(r, "Department"), S(r, "Initials"), S(r, "Color")), H);
    public async Task<bool> HasActivePractitionerAsync(Guid id, CancellationToken ct) => (await Query("SELECT COUNT(1) FROM catalog.Practitioners WHERE HospitalId=@h AND Id=@id AND Active=1", r => Convert.ToInt32(r[0]), H, ("id", id)))[0] > 0;
    public async Task<IReadOnlyList<Medicine>> LoadMedicinesAsync(CancellationToken ct) => await Query("SELECT Id,GenericName,BrandName,Strength,Form FROM catalog.Medicines WHERE HospitalId=@h AND Active=1 ORDER BY GenericName", r => new Medicine(G(r, "Id"), S(r, "GenericName"), S(r, "BrandName"), S(r, "Strength"), S(r, "Form")), H);
    public async Task<IReadOnlyList<ServiceItem>> LoadServicesAsync(CancellationToken ct) => await Query("SELECT s.Id,s.Name,s.Department,p.Amount FROM catalog.Services s CROSS APPLY (SELECT TOP(1) Amount FROM catalog.Prices WHERE HospitalId=s.HospitalId AND BranchId=@b AND ServiceId=s.Id AND EffectiveFromUtc<=SYSUTCDATETIME() AND (EffectiveToUtc IS NULL OR EffectiveToUtc>SYSUTCDATETIME()) ORDER BY EffectiveFromUtc DESC) p WHERE s.HospitalId=@h AND s.Active=1 ORDER BY s.Name", r => new ServiceItem(G(r, "Id"), S(r, "Name"), S(r, "Department"), D(r, "Amount")), H, Branch);
    public async Task<Workspace> LoadAsync(Scope s, int limit, CancellationToken ct)
    {
        var org = await Query("SELECT h.Name HospitalName,b.Name BranchName FROM org.Hospitals h JOIN org.Branches b ON b.HospitalId=h.Id WHERE h.Id=@h AND b.Id=@b", r => (S(r, "HospitalName"), S(r, "BranchName")), H, Branch);
        if (org.Count == 0) throw new DomainException("scope", "Hospital or branch is unavailable.", 403);
        var doctors = await LoadDoctorsAsync(ct);
        var services = await LoadServicesAsync(ct);
        var medicines = await LoadMedicinesAsync(ct);
        var modules = await Query("SELECT c.[Key],c.Name,c.Phase,c.Description,m.Enabled FROM org.ModuleCatalog c JOIN org.ModuleInstallations m ON m.ModuleKey=c.[Key] WHERE m.HospitalId=@h AND m.BranchId=@b ORDER BY c.Phase,c.Name", r => new ModuleSetting(S(r, "Key"), S(r, "Name"), B(r, "Enabled"), I(r, "Phase"), S(r, "Description")), H, Branch);
        var patients = s.Actor.Has(Permissions.PatientRead) ? await Query($"SELECT TOP(@limit) {PatientColumns} FROM patient.Patients WHERE HospitalId=@h AND BranchId=@b ORDER BY CreatedAtUtc DESC,Id", MapPatient, H, Branch, ("limit", limit)) : [];
        var appointments = s.Actor.Has(Permissions.ScheduleRead) ? await Query($"SELECT TOP(@limit) {AppointmentColumns} FROM sched.Appointments WHERE HospitalId=@h AND BranchId=@b ORDER BY StartsAtUtc DESC,Id", MapAppointment, H, Branch, ("limit", limit)) : [];
        var encounterFilter = "HospitalId=@h AND BranchId=@b" + (s.Actor.Has(Permissions.ClinicalSign) ? " AND DoctorId=@actor" : "");
        var encounters = s.Actor.Has(Permissions.ClinicalRead) ? await Query($"SELECT TOP(@limit) {EncounterColumns} FROM emr.Encounters WHERE {encounterFilter} ORDER BY UpdatedAtUtc DESC,Id", MapEncounter, H, Branch, ("limit", limit), ("actor", s.Actor.Id)) : [];
        if (encounters.Count > 0)
        {
            var recent = $"WITH recent AS (SELECT TOP(@limit) Id FROM emr.Encounters WHERE {encounterFilter} ORDER BY UpdatedAtUtc DESC,Id)";
            var args = new[] { H, Branch, ("limit", limit), ("actor", s.Actor.Id) };
            var allergies = await Query(recent + " SELECT a.EncounterId,a.Substance,a.Reaction,a.Severity FROM emr.Allergies a JOIN recent r ON r.Id=a.EncounterId ORDER BY a.Ordinal", r => (Id: G(r, "EncounterId"), Value: new Allergy(S(r, "Substance"), S(r, "Reaction"), S(r, "Severity"))), args);
            var meds = await Query(recent + " SELECT p.EncounterId,p.MedicineId,p.Name,p.Dose,p.Frequency,p.DurationDays,p.Instructions FROM emr.PrescriptionItems p JOIN recent r ON r.Id=p.EncounterId ORDER BY p.Ordinal", r => (Id: G(r, "EncounterId"), Value: new PrescriptionItem(G(r, "MedicineId"), S(r, "Name"), S(r, "Dose"), S(r, "Frequency"), I(r, "DurationDays"), S(r, "Instructions"))), args);
            var orders = await Query(recent + " SELECT o.EncounterId,o.Kind,o.Name,o.Instructions FROM emr.Orders o JOIN recent r ON r.Id=o.EncounterId ORDER BY o.Ordinal", r => (Id: G(r, "EncounterId"), Value: new ClinicalOrder(S(r, "Kind"), S(r, "Name"), S(r, "Instructions"))), args);
            encounters = encounters.Select(e => e with { Allergies = allergies.Where(a => a.Id == e.Id).Select(a => a.Value).ToList(), Prescription = meds.Where(a => a.Id == e.Id).Select(a => a.Value).ToList(), Orders = orders.Where(a => a.Id == e.Id).Select(a => a.Value).ToList() }).ToList();
        }
        var invoices = new List<Invoice>();
        if (s.Actor.Has(Permissions.BillingRead))
        {
            invoices = await Query("SELECT TOP(@limit) Id,Number,PatientId,EncounterId,Status,Discount,TaxRate,CreatedAtUtc,IssuedAtUtc,Version FROM bill.Invoices WHERE HospitalId=@h AND BranchId=@b ORDER BY CreatedAtUtc DESC,Id", r => new Invoice(G(r, "Id"), S(r, "Number"), G(r, "PatientId"), GN(r, "EncounterId"), S(r, "Status"), [], D(r, "Discount"), D(r, "TaxRate"), [], T(r, "CreatedAtUtc"), TN(r, "IssuedAtUtc"), L(r, "Version")), H, Branch, ("limit", limit));
            if (invoices.Count > 0)
            {
                var recent = "WITH recent AS (SELECT TOP(@limit) Id FROM bill.Invoices WHERE HospitalId=@h AND BranchId=@b ORDER BY CreatedAtUtc DESC,Id)";
                var lines = await Query(recent + " SELECT l.InvoiceId,l.ServiceId,l.Name,l.Quantity,l.UnitPrice FROM bill.Lines l JOIN recent r ON r.Id=l.InvoiceId ORDER BY l.Id", r => (Id: G(r, "InvoiceId"), Value: new InvoiceLine(G(r, "ServiceId"), S(r, "Name"), I(r, "Quantity"), D(r, "UnitPrice"))), H, Branch, ("limit", limit));
                var payments = await Query(recent + " SELECT p.InvoiceId,p.Id,p.Amount,p.Method,p.Reference,p.ReceivedAtUtc,p.ReceivedBy FROM bill.Payments p JOIN recent r ON r.Id=p.InvoiceId ORDER BY p.ReceivedAtUtc,p.Id", r => (Id: G(r, "InvoiceId"), Value: new Payment(G(r, "Id"), D(r, "Amount"), S(r, "Method"), S(r, "Reference"), T(r, "ReceivedAtUtc"), G(r, "ReceivedBy"))), H, Branch, ("limit", limit));
                invoices = invoices.Select(i => i with { Lines = lines.Where(l => l.Id == i.Id).Select(l => l.Value).ToList(), Payments = payments.Where(p => p.Id == i.Id).Select(p => p.Value).ToList() }).ToList();
            }
        }
        var audit = s.Actor.Has(Permissions.AuditRead) ? await Query("SELECT TOP(@limit) Id,ActorId,ActorName,Action,Entity,EntityId,Reason,OccurredAtUtc,ChangesJson FROM audit.AuditEvents WHERE HospitalId=@h AND BranchId=@b ORDER BY OccurredAtUtc DESC,Id", r => new AuditEvent(G(r, "Id"), G(r, "ActorId"), S(r, "ActorName"), S(r, "Action"), S(r, "Entity"), G(r, "EntityId"), S(r, "Reason"), T(r, "OccurredAtUtc"), S(r, "ChangesJson") is "" ? null : S(r, "ChangesJson")), H, Branch, ("limit", limit)) : [];
        return new(patients, appointments, encounters, invoices, doctors, services, medicines, modules, audit, org[0].Item1, org[0].Item2);
    }
    public async Task<Patient?> FindPatientAsync(Guid id, CancellationToken ct) => (await Query($"SELECT {PatientColumns} FROM patient.Patients WHERE HospitalId=@h AND Id=@id", MapPatient, H, ("id", id))).FirstOrDefault();
    public async Task<Appointment?> FindAppointmentAsync(Guid id, CancellationToken ct) => (await Query($"SELECT {AppointmentColumns} FROM sched.Appointments WHERE HospitalId=@h AND BranchId=@b AND Id=@id", MapAppointment, H, Branch, ("id", id))).FirstOrDefault();
    public async Task<Encounter?> FindEncounterAsync(Guid id, CancellationToken ct)
    {
        var records = await Query($"SELECT {EncounterColumns} FROM emr.Encounters WHERE HospitalId=@h AND BranchId=@b AND Id=@id", MapEncounter, H, Branch, ("id", id));
        return (await AttachChildren(records, "Id=@id", ct, H, ("id", id))).FirstOrDefault();
    }
    public async Task<Encounter?> FindEncounterByAppointmentAsync(Guid appointmentId, CancellationToken ct)
    {
        var records = await Query($"SELECT {EncounterColumns} FROM emr.Encounters WHERE HospitalId=@h AND BranchId=@b AND AppointmentId=@appointment", MapEncounter, H, Branch, ("appointment", appointmentId));
        return (await AttachChildren(records, "AppointmentId=@appointment", ct, H, ("appointment", appointmentId))).FirstOrDefault();
    }
    private async Task<List<Encounter>> AttachChildren(List<Encounter> records, string filter, CancellationToken ct, params (string, object?)[] args)
    {
        if (records.Count == 0) return records;
        var recent = $"WITH recent AS (SELECT TOP(@limit) Id FROM emr.Encounters WHERE HospitalId=@h AND {filter} ORDER BY UpdatedAtUtc DESC,Id)";
        var parameters = args.Append(("limit", records.Count)).ToArray();
        var allergies = await Query(recent + " SELECT a.EncounterId,a.Substance,a.Reaction,a.Severity FROM emr.Allergies a JOIN recent r ON r.Id=a.EncounterId ORDER BY a.Ordinal", r => (Id: G(r, "EncounterId"), Value: new Allergy(S(r, "Substance"), S(r, "Reaction"), S(r, "Severity"))), parameters);
        var items = await Query(recent + " SELECT p.EncounterId,p.MedicineId,p.Name,p.Dose,p.Frequency,p.DurationDays,p.Instructions FROM emr.PrescriptionItems p JOIN recent r ON r.Id=p.EncounterId ORDER BY p.Ordinal", r => (Id: G(r, "EncounterId"), Value: new PrescriptionItem(G(r, "MedicineId"), S(r, "Name"), S(r, "Dose"), S(r, "Frequency"), I(r, "DurationDays"), S(r, "Instructions"))), parameters);
        var orders = await Query(recent + " SELECT o.EncounterId,o.Kind,o.Name,o.Instructions FROM emr.Orders o JOIN recent r ON r.Id=o.EncounterId ORDER BY o.Ordinal", r => (Id: G(r, "EncounterId"), Value: new ClinicalOrder(S(r, "Kind"), S(r, "Name"), S(r, "Instructions"))), parameters);
        return records.Select(e => e with { Allergies = allergies.Where(x => x.Id == e.Id).Select(x => x.Value).ToList(), Prescription = items.Where(x => x.Id == e.Id).Select(x => x.Value).ToList(), Orders = orders.Where(x => x.Id == e.Id).Select(x => x.Value).ToList() }).ToList();
    }
    public async Task<Invoice?> FindInvoiceAsync(Guid id, CancellationToken ct)
    {
        var invoice = (await Query("SELECT Id,Number,PatientId,EncounterId,Status,Discount,TaxRate,CreatedAtUtc,IssuedAtUtc,Version FROM bill.Invoices WHERE HospitalId=@h AND BranchId=@b AND Id=@id", r => new Invoice(G(r, "Id"), S(r, "Number"), G(r, "PatientId"), GN(r, "EncounterId"), S(r, "Status"), [], D(r, "Discount"), D(r, "TaxRate"), [], T(r, "CreatedAtUtc"), TN(r, "IssuedAtUtc"), L(r, "Version")), H, Branch, ("id", id))).FirstOrDefault();
        if (invoice is null) return null;
        var lines = await Query("SELECT ServiceId,Name,Quantity,UnitPrice FROM bill.Lines WHERE InvoiceId=@id ORDER BY Id", r => new InvoiceLine(G(r, "ServiceId"), S(r, "Name"), I(r, "Quantity"), D(r, "UnitPrice")), ("id", id));
        var payments = await Query("SELECT Id,Amount,Method,Reference,ReceivedAtUtc,ReceivedBy FROM bill.Payments WHERE InvoiceId=@id ORDER BY ReceivedAtUtc,Id", r => new Payment(G(r, "Id"), D(r, "Amount"), S(r, "Method"), S(r, "Reference"), T(r, "ReceivedAtUtc"), G(r, "ReceivedBy")), ("id", id));
        return invoice with { Lines = lines, Payments = payments };
    }
    public async Task<ModuleSetting?> FindModuleAsync(string key, CancellationToken ct) => (await Query("SELECT c.[Key],c.Name,c.Phase,c.Description,m.Enabled FROM org.ModuleCatalog c JOIN org.ModuleInstallations m ON m.ModuleKey=c.[Key] WHERE m.HospitalId=@h AND m.BranchId=@b AND c.[Key]=@key", r => new ModuleSetting(S(r, "Key"), S(r, "Name"), B(r, "Enabled"), I(r, "Phase"), S(r, "Description")), H, Branch, ("key", key))).FirstOrDefault();
    public async Task<bool> HasAppointmentConflictAsync(Guid doctorId, DateTimeOffset start, DateTimeOffset end, Guid? excludeId, CancellationToken ct)
    {
        await using var c = Command("SELECT COUNT(1) FROM sched.Appointments WHERE HospitalId=@h AND BranchId=@b AND DoctorId=@doctor AND Status<>'Cancelled' AND StartsAtUtc<@end AND DATEADD(minute,DurationMinutes,StartsAtUtc)>@start AND (@exclude IS NULL OR Id<>@exclude)", H, Branch, ("doctor", doctorId), ("start", start.UtcDateTime), ("end", end.UtcDateTime), ("exclude", excludeId));
        return Convert.ToInt32(await c.ExecuteScalarAsync(ct)) > 0;
    }
    public async Task<Chart?> LoadChartAsync(Guid patientId, int limit, CancellationToken ct)
    {
        var patient = await FindPatientAsync(patientId, ct); if (patient is null) return null;
        var take = limit + 1; var patientArg = ("patient", patientId);
        var clinicalFilter = "PatientId=@patient" + (scope.Actor.Has(Permissions.ClinicalSign) ? " AND DoctorId=@actor" : "");
        var encounters = scope.Actor.Has(Permissions.ClinicalRead) ? await Query($"SELECT TOP(@limit) {EncounterColumns} FROM emr.Encounters WHERE HospitalId=@h AND BranchId=@b AND {clinicalFilter} ORDER BY UpdatedAtUtc DESC,Id", MapEncounter, H, Branch, patientArg, ("limit", take), ("actor", scope.Actor.Id)) : [];
        encounters = await AttachChildren(encounters, clinicalFilter, ct, H, patientArg, ("actor", scope.Actor.Id));
        var appointments = scope.Actor.Has(Permissions.ScheduleRead) ? await Query($"SELECT TOP(@limit) {AppointmentColumns} FROM sched.Appointments WHERE HospitalId=@h AND BranchId=@b AND PatientId=@patient ORDER BY StartsAtUtc DESC,Id", MapAppointment, H, Branch, patientArg, ("limit", take)) : [];
        var invoices = new List<Invoice>();
        if (scope.Actor.Has(Permissions.BillingRead))
        {
            var ids = await Query("SELECT TOP(@limit) Id FROM bill.Invoices WHERE HospitalId=@h AND BranchId=@b AND PatientId=@patient ORDER BY CreatedAtUtc DESC,Id", r => G(r, "Id"), H, Branch, patientArg, ("limit", take));
            foreach (var id in ids) if (await FindInvoiceAsync(id, ct) is { } invoice) invoices.Add(invoice);
        }
        return new Chart(patient, encounters.Take(limit).ToList(), appointments.Take(limit).ToList(), invoices, encounters.Count > limit || appointments.Count > limit || invoices.Count > limit);
    }
    public Task<IReadOnlyList<Patient>> FindDuplicatesAsync(string phone, string first, string last, DateOnly dob, CancellationToken ct) => FindDuplicates(phone, first, last, dob, ct);
    private async Task<IReadOnlyList<Patient>> FindDuplicates(string phone, string first, string last, DateOnly dob, CancellationToken ct)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        return await Query($"SELECT TOP(10) {PatientColumns} FROM patient.Patients WHERE HospitalId=@h AND Active=1 AND ((@digits<>'' AND LEN(@digits)>=7 AND REPLACE(REPLACE(REPLACE(REPLACE(Phone,' ',''),'-',''),'(',''),')','') LIKE '%'+RIGHT(@digits,7)) OR (LOWER(FirstName)=LOWER(@first) AND LOWER(LastName)=LOWER(@last) AND DateOfBirth=@dob))", MapPatient, H, ("digits", digits), ("first", first.Trim()), ("last", last.Trim()), ("dob", dob));
    }
    public async Task<string> NextNumberAsync(string kind, string key, string prefix, CancellationToken ct)
    {
        await Execute("IF NOT EXISTS(SELECT 1 FROM org.NumberSequences WITH(UPDLOCK,HOLDLOCK) WHERE HospitalId=@h AND Kind=@kind AND ScopeKey=@key) INSERT INTO org.NumberSequences(HospitalId,Kind,ScopeKey,Prefix,CurrentValue) VALUES(@h,@kind,@key,@prefix,0)", H, ("kind", kind), ("key", key), ("prefix", prefix));
        await using var c = Command("UPDATE org.NumberSequences SET CurrentValue=CurrentValue+1 OUTPUT inserted.CurrentValue WHERE HospitalId=@h AND Kind=@kind AND ScopeKey=@key", H, ("kind", kind), ("key", key));
        var value = Convert.ToInt64(await c.ExecuteScalarAsync(ct)); return prefix.Length == 0 ? value.ToString() : $"{prefix}-{value.ToString(kind == "UHID" ? "D6" : "D4")}";
    }
    public Task InsertPatientAsync(Patient p, CancellationToken ct) => Execute("INSERT INTO patient.Patients(Id,HospitalId,BranchId,Uhid,FirstName,LastName,DateOfBirth,Sex,Phone,Email,BloodGroup,Address,EmergencyContact,CreatedAtUtc,CreatedBy,Active,Version) VALUES(@id,@h,@b,@uhid,@first,@last,@dob,@sex,@phone,@email,@blood,@address,@emergency,@at,@actor,1,1)",
        ("id", p.Id), H, Branch, ("uhid", p.Uhid), ("first", p.FirstName), ("last", p.LastName), ("dob", p.DateOfBirth), ("sex", p.Sex), ("phone", p.Phone), ("email", p.Email), ("blood", p.BloodGroup), ("address", p.Address), ("emergency", p.EmergencyContact), ("at", p.CreatedAt.UtcDateTime), ("actor", scope.Actor.Id));
    public async Task UpdatePatientAsync(Patient p, CancellationToken ct)
    {
        if (await Execute("UPDATE patient.Patients SET FirstName=@first,LastName=@last,DateOfBirth=@dob,Sex=@sex,Phone=@phone,Email=@email,BloodGroup=@blood,Address=@address,EmergencyContact=@emergency,Version=@new,UpdatedAtUtc=SYSUTCDATETIME(),UpdatedBy=@actor WHERE Id=@id AND HospitalId=@h AND Version=@old", ("first", p.FirstName), ("last", p.LastName), ("dob", p.DateOfBirth), ("sex", p.Sex), ("phone", p.Phone), ("email", p.Email), ("blood", p.BloodGroup), ("address", p.Address), ("emergency", p.EmergencyContact), ("new", p.Version), ("actor", scope.Actor.Id), ("id", p.Id), H, ("old", p.Version - 1)) != 1) throw Conflict();
    }
    public Task InsertAppointmentAsync(Appointment a, CancellationToken ct) => Execute($"INSERT INTO sched.Appointments(Id,HospitalId,BranchId,PatientId,DoctorId,StartsAtUtc,DurationMinutes,Mode,Reason,Status,Token,CreatedAtUtc,CreatedBy,Version) VALUES(@id,@h,@b,@patient,@doctor,@at,@duration,@mode,@reason,@status,@token,@created,@actor,1)", ("id", a.Id), H, Branch, ("patient", a.PatientId), ("doctor", a.DoctorId), ("at", a.StartsAt.UtcDateTime), ("duration", a.DurationMinutes), ("mode", a.Mode), ("reason", a.Reason), ("status", a.Status), ("token", a.Token), ("created", a.CreatedAt.UtcDateTime), ("actor", scope.Actor.Id));
    public async Task UpdateAppointmentAsync(Appointment a, CancellationToken ct)
    {
        if (await Execute("UPDATE sched.Appointments SET StartsAtUtc=@at,Status=@status,Token=@token,Version=@new WHERE Id=@id AND HospitalId=@h AND Version=@old", ("at", a.StartsAt.UtcDateTime), ("status", a.Status), ("token", a.Token), ("new", a.Version), ("id", a.Id), H, ("old", a.Version - 1)) != 1) throw Conflict();
        if (a.Token.HasValue) await Execute("IF NOT EXISTS(SELECT 1 FROM sched.QueueEntries WHERE AppointmentId=@id) INSERT INTO sched.QueueEntries(Id,HospitalId,AppointmentId,Token,Status,EnteredAtUtc) VALUES(NEWID(),@h,@id,@token,@status,SYSUTCDATETIME())", ("id", a.Id), H, ("token", a.Token), ("status", a.Status));
        else await Execute("UPDATE sched.QueueEntries SET Status=@status WHERE AppointmentId=@id", ("id", a.Id), ("status", a.Status));
    }
    public async Task InsertEncounterAsync(Encounter e, CancellationToken ct)
    {
        await Execute($"INSERT INTO emr.Encounters({EncounterColumns.Replace("[Plan],", "[Plan],")},HospitalId,BranchId) VALUES(@id,@patient,@appointment,@doctor,@status,@complaint,@exam,@diagnosis,@plan,@follow,@temp,@pulse,@sys,@dia,@weight,@spo2,@reviewed,@signedBy,@signedAt,@hash,@updated,@version,@h,@b)", [.. EncounterArgs(e), H, Branch]);
        await SaveChildren(e, ct); await SaveVersion(e, ct);
    }
    private (string, object?)[] EncounterArgs(Encounter e) => [("id", e.Id), ("patient", e.PatientId), ("appointment", e.AppointmentId), ("doctor", e.DoctorId), ("status", e.Status), ("complaint", e.ChiefComplaint), ("exam", e.Examination), ("diagnosis", e.Diagnosis), ("plan", e.Plan), ("follow", e.FollowUp), ("temp", e.Vitals.TemperatureC), ("pulse", e.Vitals.Pulse), ("sys", e.Vitals.Systolic), ("dia", e.Vitals.Diastolic), ("weight", e.Vitals.WeightKg), ("spo2", e.Vitals.Spo2), ("reviewed", e.AllergyReviewed), ("signedBy", e.SignedBy), ("signedAt", e.SignedAt?.UtcDateTime), ("hash", e.ContentHash), ("updated", e.UpdatedAt.UtcDateTime), ("version", e.Version)];
    public async Task UpdateEncounterAsync(Encounter e, CancellationToken ct)
    {
        // A draft may be revised; Draft→Signed is a one-way, audited transition.
        if (await Execute("UPDATE emr.Encounters SET Status=@status,ChiefComplaint=@complaint,Examination=@exam,Diagnosis=@diagnosis,[Plan]=@plan,FollowUp=@follow,TemperatureC=@temp,Pulse=@pulse,Systolic=@sys,Diastolic=@dia,WeightKg=@weight,Spo2=@spo2,AllergyReviewed=@reviewed,SignedBy=@signedBy,SignedAtUtc=@signedAt,ContentHash=@hash,UpdatedAtUtc=@updated,Version=@version WHERE Id=@id AND HospitalId=@h AND Version=@old AND Status='Draft'", EncounterArgs(e).Append(H).Append(("old", e.Version - 1)).ToArray()) != 1) throw Conflict();
        await Execute("DELETE FROM emr.Allergies WHERE EncounterId=@id; DELETE FROM emr.PrescriptionItems WHERE EncounterId=@id; DELETE FROM emr.Orders WHERE EncounterId=@id", ("id", e.Id));
        await SaveChildren(e, ct); await SaveVersion(e, ct);
    }
    private async Task SaveChildren(Encounter e, CancellationToken ct)
    {
        for (var i = 0; i < e.Allergies.Count; i++) { var a = e.Allergies[i]; await Execute("INSERT INTO emr.Allergies(EncounterId,HospitalId,Ordinal,Substance,Reaction,Severity) VALUES(@id,@h,@ordinal,@substance,@reaction,@severity)", ("id", e.Id), H, ("ordinal", i), ("substance", a.Substance), ("reaction", a.Reaction), ("severity", a.Severity)); }
        for (var i = 0; i < e.Prescription.Count; i++) { var p = e.Prescription[i]; await Execute("INSERT INTO emr.PrescriptionItems(EncounterId,HospitalId,Ordinal,MedicineId,Name,Dose,Frequency,DurationDays,Instructions) VALUES(@id,@h,@ordinal,@medicine,@name,@dose,@frequency,@days,@instructions)", ("id", e.Id), H, ("ordinal", i), ("medicine", p.MedicineId), ("name", p.Name), ("dose", p.Dose), ("frequency", p.Frequency), ("days", p.DurationDays), ("instructions", p.Instructions)); }
        for (var i = 0; i < e.Orders.Count; i++) { var o = e.Orders[i]; await Execute("INSERT INTO emr.Orders(Id,HospitalId,EncounterId,Ordinal,Kind,Name,Instructions) VALUES(NEWID(),@h,@id,@ordinal,@kind,@name,@instructions)", H, ("id", e.Id), ("ordinal", i), ("kind", o.Kind), ("name", o.Name), ("instructions", o.Instructions)); }
    }
    private Task SaveVersion(Encounter e, CancellationToken ct) => Execute("INSERT INTO emr.RecordVersions(Id,HospitalId,EncounterId,Version,SnapshotJson,ContentHash,CreatedBy,CreatedAtUtc) VALUES(NEWID(),@h,@id,@version,@snapshot,@hash,@actor,@at)", H, ("id", e.Id), ("version", e.Version), ("snapshot", JsonSerializer.Serialize(e)), ("hash", e.ContentHash), ("actor", scope.Actor.Id), ("at", e.UpdatedAt.UtcDateTime));
    public async Task InsertInvoiceAsync(Invoice invoice, CancellationToken ct)
    {
        await Execute("INSERT INTO bill.Invoices(Id,HospitalId,BranchId,Number,PatientId,EncounterId,Status,Discount,TaxRate,CreatedAtUtc,IssuedAtUtc,CreatedBy,Version) VALUES(@id,@h,@b,@number,@patient,@encounter,@status,@discount,@tax,@created,@issued,@actor,1)", ("id", invoice.Id), H, Branch, ("number", invoice.Number), ("patient", invoice.PatientId), ("encounter", invoice.EncounterId), ("status", invoice.Status), ("discount", invoice.Discount), ("tax", invoice.TaxRate), ("created", invoice.CreatedAt.UtcDateTime), ("issued", invoice.IssuedAt?.UtcDateTime), ("actor", scope.Actor.Id));
        foreach (var line in invoice.Lines) await Execute("INSERT INTO bill.Lines(Id,HospitalId,InvoiceId,ServiceId,Name,Quantity,UnitPrice) VALUES(NEWID(),@h,@id,@service,@name,@quantity,@price)", H, ("id", invoice.Id), ("service", line.ServiceId), ("name", line.Name), ("quantity", line.Quantity), ("price", line.UnitPrice));
    }
    public async Task UpdateInvoiceAsync(Invoice invoice, CancellationToken ct)
    {
        if (await Execute("UPDATE bill.Invoices SET Status=@status,IssuedAtUtc=@issued,Version=@new WHERE Id=@id AND HospitalId=@h AND Version=@old", ("status", invoice.Status), ("issued", invoice.IssuedAt?.UtcDateTime), ("new", invoice.Version), ("id", invoice.Id), H, ("old", invoice.Version - 1)) != 1) throw Conflict();
        var existing = (await Query("SELECT Id FROM bill.Payments WHERE InvoiceId=@id", r => G(r, "Id"), ("id", invoice.Id))).ToHashSet();
        foreach (var p in invoice.Payments.Where(p => !existing.Contains(p.Id))) await Execute("INSERT INTO bill.Payments(Id,HospitalId,InvoiceId,Amount,Method,Reference,ReceivedAtUtc,ReceivedBy) VALUES(@id,@h,@invoice,@amount,@method,@reference,@at,@actor)", ("id", p.Id), H, ("invoice", invoice.Id), ("amount", p.Amount), ("method", p.Method), ("reference", p.Reference), ("at", p.ReceivedAt.UtcDateTime), ("actor", p.ReceivedBy));
    }
    public Task SetModuleAsync(string key, bool enabled, CancellationToken ct) => Execute("UPDATE org.ModuleInstallations SET Enabled=@enabled,UpdatedAtUtc=SYSUTCDATETIME(),UpdatedBy=@actor WHERE HospitalId=@h AND BranchId=@b AND ModuleKey=@key", ("enabled", enabled), ("actor", scope.Actor.Id), H, Branch, ("key", key));
    public Task AddAuditAsync(AuditEvent a, CancellationToken ct) => Execute("INSERT INTO audit.AuditEvents(Id,HospitalId,BranchId,ActorId,ActorName,Action,Entity,EntityId,Reason,ChangesJson,OccurredAtUtc) VALUES(@id,@h,@b,@actor,@name,@action,@entity,@entityId,@reason,@changes,@at)", ("id", a.Id), H, Branch, ("actor", a.ActorId), ("name", a.ActorName), ("action", a.Action), ("entity", a.Entity), ("entityId", a.EntityId), ("reason", a.Reason), ("changes", a.Changes), ("at", a.OccurredAt.UtcDateTime));
    public Task AddOutboxAsync(string type, Guid id, string payload, CancellationToken ct) => Execute("INSERT INTO integration.OutboxMessages(Id,HospitalId,BranchId,Type,AggregateId,PayloadJson,NextAttemptAtUtc) VALUES(NEWID(),@h,@b,@type,@id,@payload,SYSUTCDATETIME())", H, Branch, ("type", type), ("id", id), ("payload", payload));
    private const string UserColumns = "u.Id,u.UserName,u.DisplayName,u.PasswordHash,u.HospitalId,u.Enabled,r.Name Role,m.BranchId";
    private static UserAccount MapUser(DbDataReader r) => new(G(r, "Id"), S(r, "UserName"), S(r, "DisplayName"), S(r, "PasswordHash"), S(r, "Role"), G(r, "HospitalId"), G(r, "BranchId"), B(r, "Enabled"));
    private async Task<UserAccount?> WithGrants(UserAccount? user, CancellationToken ct)
    {
        if (user is null) return null;
        var grants = await Query("SELECT DISTINCT rp.PermissionKey FROM iam.Memberships m JOIN iam.UserRoles ur ON ur.MembershipId=m.Id JOIN iam.RolePermissions rp ON rp.RoleId=ur.RoleId AND rp.HospitalId=m.HospitalId WHERE m.UserId=@id AND m.HospitalId=@h AND m.BranchId=@b AND m.Active=1", r => S(r, "PermissionKey"), ("id", user.Id), ("h", user.HospitalId), ("b", user.BranchId));
        return user with { Grants = grants };
    }
    public async Task<UserAccount?> FindUserAsync(string userName, Guid branchId, CancellationToken ct) => await WithGrants((await Query($"SELECT TOP(1) {UserColumns} FROM iam.Users u JOIN iam.Memberships m ON m.UserId=u.Id AND m.HospitalId=u.HospitalId JOIN iam.UserRoles ur ON ur.MembershipId=m.Id JOIN iam.Roles r ON r.Id=ur.RoleId WHERE u.UserName=@name AND u.HospitalId=@h AND m.BranchId=@b AND m.Active=1 ORDER BY r.Name", MapUser, ("name", userName), H, ("b", branchId))).FirstOrDefault(), ct);
    public async Task<UserAccount?> FindUserAsync(Guid id, Guid hospitalId, Guid branchId, CancellationToken ct) => await WithGrants((await Query($"SELECT TOP(1) {UserColumns} FROM iam.Users u JOIN iam.Memberships m ON m.UserId=u.Id AND m.HospitalId=u.HospitalId JOIN iam.UserRoles ur ON ur.MembershipId=m.Id JOIN iam.Roles r ON r.Id=ur.RoleId WHERE u.Id=@id AND u.HospitalId=@h AND m.BranchId=@b AND m.Active=1 ORDER BY r.Name", MapUser, ("id", id), H, ("b", branchId))).FirstOrDefault(), ct);
    public async Task<UserSession?> FindSessionAsync(string hash, CancellationToken ct) => (await Query("SELECT Id,UserId,TokenHash,ExpiresAtUtc,RevokedAtUtc FROM iam.Sessions WHERE TokenHash=@hash", r => new UserSession(G(r, "Id"), G(r, "UserId"), S(r, "TokenHash"), T(r, "ExpiresAtUtc"), TN(r, "RevokedAtUtc")), ("hash", hash))).FirstOrDefault();
    public Task CreateSessionAsync(UserSession s, CancellationToken ct) => Execute("INSERT INTO iam.Sessions(Id,HospitalId,UserId,TokenHash,ExpiresAtUtc) VALUES(@id,@h,@user,@hash,@expiry)", ("id", s.Id), H, ("user", s.UserId), ("hash", s.TokenHash), ("expiry", s.ExpiresAt.UtcDateTime));
    public Task RevokeSessionAsync(Guid id, CancellationToken ct) => Execute("UPDATE iam.Sessions SET RevokedAtUtc=SYSUTCDATETIME() WHERE Id=@id AND RevokedAtUtc IS NULL", ("id", id));
    public Task UpdatePasswordAsync(Guid id, string hash, CancellationToken ct) => Execute("UPDATE iam.Users SET PasswordHash=@hash,UpdatedAtUtc=SYSUTCDATETIME() WHERE Id=@id; UPDATE iam.Sessions SET RevokedAtUtc=SYSUTCDATETIME() WHERE UserId=@id AND RevokedAtUtc IS NULL", ("hash", hash), ("id", id));
    public Task RecordLoginAsync(Guid? userId, string userName, bool success, string? sourceIp, CancellationToken ct) => Execute("INSERT INTO iam.LoginEvents(HospitalId,UserId,UserName,Success,SourceIp) VALUES(@h,@user,@name,@success,@ip)", H, ("user", userId), ("name", userName), ("success", success), ("ip", sourceIp));
}
