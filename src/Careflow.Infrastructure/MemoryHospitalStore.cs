using System.Text.Json;
using Careflow.Application;
using Careflow.Domain;
namespace Careflow.Infrastructure;

/// <summary>Explicit development/test adapter only. Composition refuses it outside Demo mode.</summary>
public sealed class MemoryUnitOfWork(DemoData initial) : IUnitOfWork, IDisposable
{
    private DemoData data = initial;
    private readonly SemaphoreSlim gate = new(1, 1);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public async Task<T> ExecuteAsync<T>(Scope scope, Func<IHospitalStore, Task<T>> action, CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            // Clone the entire small synthetic dataset so a failed use case leaves no partial writes.
            var candidate = JsonSerializer.Deserialize<DemoData>(JsonSerializer.Serialize(data, Json), Json)!;
            var result = await action(new MemoryHospitalStore(candidate, scope));
            // Preserve the injected instance while atomically publishing the cloned write set.
            data.Patients = candidate.Patients; data.Appointments = candidate.Appointments; data.Encounters = candidate.Encounters;
            data.Invoices = candidate.Invoices; data.Modules = candidate.Modules; data.Audit = candidate.Audit;
            data.Sessions = candidate.Sessions; data.Users = candidate.Users; data.Sequences = candidate.Sequences;
            data.Logins = candidate.Logins; data.Outbox = candidate.Outbox;
            data.Beds = candidate.Beds; data.Admissions = candidate.Admissions; data.BedEvents = candidate.BedEvents;
            data.NursingNotes = candidate.NursingNotes; data.MedicationOrders = candidate.MedicationOrders; data.Administrations = candidate.Administrations;
            data.Handovers = candidate.Handovers; data.StockBatches = candidate.StockBatches; data.StockMovements = candidate.StockMovements; data.Dispenses = candidate.Dispenses;
            data.LabTests = candidate.LabTests; data.LabOrders = candidate.LabOrders; data.LabResults = candidate.LabResults; data.CriticalAlerts = candidate.CriticalAlerts;
            data.Policies = candidate.Policies;
            data.RadiologyOrders = candidate.RadiologyOrders; data.RadiologyReports = candidate.RadiologyReports;
            data.EmergencyRegistrations = candidate.EmergencyRegistrations; data.TriageAssessments = candidate.TriageAssessments;
            data.EmergencyObservations = candidate.EmergencyObservations; data.EmergencyDispositions = candidate.EmergencyDispositions;
            data.OtBookings = candidate.OtBookings; data.OtChecklists = candidate.OtChecklists;
            data.BloodUnits = candidate.BloodUnits; data.BloodScreenings = candidate.BloodScreenings;
            data.Crossmatches = candidate.Crossmatches; data.BloodIssues = candidate.BloodIssues;
            return result;
        }
        finally { gate.Release(); }
    }
    public void Dispose() => gate.Dispose();
}
public sealed partial class MemoryHospitalStore(DemoData data, Scope scope) : IHospitalStore
{
    private static T Missing<T>() => throw new DomainException("not_found", "Record not found in this hospital.", 404);
    private static void Conflict() => throw new DomainException("concurrency", "Record version changed. Refresh before retrying.", 409);
    public Task<Workspace> LoadAsync(Scope s, int limit, CancellationToken ct) => Task.FromResult(data.Snapshot(limit));
    public Task<Patient?> FindPatientAsync(Guid id, CancellationToken ct) => Task.FromResult(data.Patients.FirstOrDefault(x => x.Id == id));
    public Task<Appointment?> FindAppointmentAsync(Guid id, CancellationToken ct) => Task.FromResult(data.Appointments.FirstOrDefault(x => x.Id == id));
    public Task<Encounter?> FindEncounterAsync(Guid id, CancellationToken ct) => Task.FromResult(data.Encounters.FirstOrDefault(x => x.Id == id && (!scope.Actor.Has(Permissions.ClinicalSign) || x.DoctorId == scope.Actor.Id)));
    public Task<Encounter?> FindEncounterByAppointmentAsync(Guid id, CancellationToken ct) => Task.FromResult(data.Encounters.FirstOrDefault(x => x.AppointmentId == id));
    public Task<Invoice?> FindInvoiceAsync(Guid id, CancellationToken ct) => Task.FromResult(data.Invoices.FirstOrDefault(x => x.Id == id));
    public Task<ModuleSetting?> FindModuleAsync(string key, CancellationToken ct) => Task.FromResult(data.Modules.FirstOrDefault(x => x.Key == key));
    public Task<bool> HasActivePractitionerAsync(Guid id, CancellationToken ct) => Task.FromResult(data.Doctors.Any(x => x.Id == id));
    public Task<IReadOnlyList<Medicine>> LoadMedicinesAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<Medicine>>(data.Medicines);
    public Task<IReadOnlyList<ServiceItem>> LoadServicesAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<ServiceItem>>(data.Services);
    public Task<bool> HasAppointmentConflictAsync(Guid doctorId, DateTimeOffset start, DateTimeOffset end, Guid? excludeId, CancellationToken ct) => Task.FromResult(data.Appointments.Any(a => a.Id != excludeId && a.DoctorId == doctorId && a.Status != "Cancelled" && a.StartsAt < end && a.StartsAt.AddMinutes(a.DurationMinutes) > start));
    public Task<Chart?> LoadChartAsync(Guid patientId, int limit, CancellationToken ct)
    {
        var patient = data.Patients.FirstOrDefault(p => p.Id == patientId); if (patient is null) return Task.FromResult<Chart?>(null);
        var encounters = data.Encounters.Where(e => e.PatientId == patientId && scope.Actor.Has(Permissions.ClinicalRead) && (!scope.Actor.Has(Permissions.ClinicalSign) || e.DoctorId == scope.Actor.Id)).OrderByDescending(e => e.UpdatedAt).ToList();
        var appointments = data.Appointments.Where(a => a.PatientId == patientId && scope.Actor.Has(Permissions.ScheduleRead)).OrderByDescending(a => a.StartsAt).ToList();
        var invoices = data.Invoices.Where(i => i.PatientId == patientId && scope.Actor.Has(Permissions.BillingRead)).OrderByDescending(i => i.CreatedAt).ToList();
        return Task.FromResult<Chart?>(new Chart(patient, encounters.Take(limit).ToList(), appointments.Take(limit).ToList(), invoices.Take(limit).ToList(), new[] { encounters.Count, appointments.Count, invoices.Count }.Any(n => n > limit)));
    }
    public Task<IReadOnlyList<Patient>> FindDuplicatesAsync(string phone, string first, string last, DateOnly dob, CancellationToken ct)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        return Task.FromResult<IReadOnlyList<Patient>>(data.Patients.Where(p => (digits.Length >= 7 && new string(p.Phone.Where(char.IsDigit).ToArray()).EndsWith(digits[^7..], StringComparison.Ordinal)) || (p.FirstName.Equals(first.Trim(), StringComparison.OrdinalIgnoreCase) && p.LastName.Equals(last.Trim(), StringComparison.OrdinalIgnoreCase) && p.DateOfBirth == dob)).ToList());
    }
    public Task<string> NextNumberAsync(string kind, string key, string prefix, CancellationToken ct)
    {
        var k = kind + ":" + key; var next = data.Sequences.GetValueOrDefault(k) + 1; data.Sequences[k] = next;
        return Task.FromResult(prefix.Length == 0 ? next.ToString() : $"{prefix}-{next.ToString(kind == "UHID" ? "D6" : "D4")}");
    }
    public Task InsertPatientAsync(Patient p, CancellationToken ct) { data.Patients.Add(p); return Task.CompletedTask; }
    public Task UpdatePatientAsync(Patient p, CancellationToken ct) { var old = data.Patients.FirstOrDefault(x => x.Id == p.Id) ?? Missing<Patient>(); if (old.Version != p.Version - 1) Conflict(); data.Patients[data.Patients.IndexOf(old)] = p; return Task.CompletedTask; }
    public Task InsertAppointmentAsync(Appointment a, CancellationToken ct) { data.Appointments.Add(a); return Task.CompletedTask; }
    public Task UpdateAppointmentAsync(Appointment a, CancellationToken ct) { var old = data.Appointments.FirstOrDefault(x => x.Id == a.Id) ?? Missing<Appointment>(); if (old.Version != a.Version - 1) Conflict(); data.Appointments[data.Appointments.IndexOf(old)] = a; return Task.CompletedTask; }
    public Task InsertEncounterAsync(Encounter e, CancellationToken ct) { data.Encounters.Add(e); return Task.CompletedTask; }
    public Task UpdateEncounterAsync(Encounter e, CancellationToken ct) { var old = data.Encounters.FirstOrDefault(x => x.Id == e.Id) ?? Missing<Encounter>(); if (old.Version != e.Version - 1 || old.Status != "Draft") Conflict(); data.Encounters[data.Encounters.IndexOf(old)] = e; return Task.CompletedTask; }
    public Task InsertInvoiceAsync(Invoice i, CancellationToken ct) { data.Invoices.Add(i); return Task.CompletedTask; }
    public Task UpdateInvoiceAsync(Invoice i, CancellationToken ct) { var old = data.Invoices.FirstOrDefault(x => x.Id == i.Id) ?? Missing<Invoice>(); if (old.Version != i.Version - 1 || old.Status == "Draft" && i.Status != "Issued") Conflict(); if (data.Invoices.Where(x => x.Id != i.Id).SelectMany(x => x.Payments).Any(p => i.Payments.Any(n => n.Method == p.Method && n.Reference == p.Reference))) throw new DomainException("payment.idempotency", "This payment reference is already allocated.", 409); data.Invoices[data.Invoices.IndexOf(old)] = i; return Task.CompletedTask; }
    public Task SetModuleAsync(string key, bool enabled, CancellationToken ct) { var m = data.Modules.FirstOrDefault(x => x.Key == key) ?? Missing<ModuleSetting>(); data.Modules[data.Modules.IndexOf(m)] = m with { Enabled = enabled }; return Task.CompletedTask; }
    public Task AddAuditAsync(AuditEvent a, CancellationToken ct) { data.Audit.Add(a); return Task.CompletedTask; }
    public Task AddOutboxAsync(string type, Guid id, string payload, CancellationToken ct) { data.Outbox.Add(new OutboxEntry(Guid.NewGuid(), type, id, payload)); return Task.CompletedTask; }
    public Task<UserAccount?> FindUserAsync(string userName, Guid branchId, CancellationToken ct) => Task.FromResult(data.Users.FirstOrDefault(x => x.UserName == userName && x.HospitalId == scope.HospitalId && x.BranchId == branchId));
    public Task<UserAccount?> FindUserAsync(Guid id, Guid hospitalId, Guid branchId, CancellationToken ct) => Task.FromResult(data.Users.FirstOrDefault(x => x.Id == id && x.HospitalId == hospitalId && x.BranchId == branchId));
    public Task<UserSession?> FindSessionAsync(string hash, CancellationToken ct) => Task.FromResult(data.Sessions.FirstOrDefault(x => x.TokenHash == hash));
    public Task CreateSessionAsync(UserSession session, CancellationToken ct) { data.Sessions.Add(session); return Task.CompletedTask; }
    public Task RevokeSessionAsync(Guid id, CancellationToken ct) { data.Sessions = data.Sessions.Select(s => s.Id == id ? s with { RevokedAt = DateTimeOffset.UtcNow } : s).ToList(); return Task.CompletedTask; }
    public Task UpdatePasswordAsync(Guid id, string hash, CancellationToken ct) { data.Users = data.Users.Select(u => u.Id == id ? u with { PasswordHash = hash } : u).ToList(); data.Sessions = data.Sessions.Select(s => s.UserId == id ? s with { RevokedAt = DateTimeOffset.UtcNow } : s).ToList(); return Task.CompletedTask; }
    public Task RecordLoginAsync(Guid? userId, string userName, bool success, string? sourceIp, CancellationToken ct) { data.Logins.Add(new LoginAttempt(userId, userName, success, DateTimeOffset.UtcNow, sourceIp)); return Task.CompletedTask; }
}
