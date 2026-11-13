using System.Security.Cryptography;
using System.Text.Json;
using Careflow.Domain;
namespace Careflow.Application;

public sealed class HospitalService(IUnitOfWork unitOfWork, IClock clock, IPasswordService passwords)
{
    private readonly string dummyHash = passwords.Hash(Guid.NewGuid().ToString());
    private static Task<T> Missing<T>(string entity) => throw new DomainException("not_found", $"{entity} was not found in your hospital.", 404);
    private static void Conflict() => throw new DomainException("concurrency", "This record changed in another session. Refresh and try again.", 409);
    private static async Task RequireModule(IHospitalStore s, string key, string message, CancellationToken ct)
    {
        if (await s.FindModuleAsync(key, ct) is not { Enabled: true }) throw new DomainException("module.disabled", message);
    }
    private static void ValidatePatientDetails(PatientInput input)
    {
        if (input.Email.Length > 254 || input.Address.Length > 500 || input.EmergencyContact.Length > 200 || input.DuplicateReviewReason.Length > 1000 || !new[] { "Unknown", "A+", "A-", "B+", "B-", "AB+", "AB-", "O+", "O-" }.Contains(input.BloodGroup) || input.Email.Length > 0 && !System.Net.Mail.MailAddress.TryCreate(input.Email, out _))
            throw new DomainException("patient.fields", "Review contact fields, blood group and permitted lengths.");
    }
    public async Task<WorkspaceResult> GetWorkspace(Scope scope, CancellationToken ct)
    {
        scope.Require(Permissions.PatientRead);
        return await unitOfWork.ExecuteAsync(scope, async s =>
        {
            var w = await s.LoadAsync(scope, 201, ct);
            var p = scope.Actor.Permissions;
            return new WorkspaceResult(w with
            {
                Patients = p.Contains(Permissions.PatientRead) ? w.Patients.Take(200).ToList() : [],
                Encounters = p.Contains(Permissions.ClinicalRead) ? w.Encounters.Where(e => !p.Contains(Permissions.ClinicalSign) || e.DoctorId == scope.Actor.Id).Take(200).ToList() : [],
                Invoices = p.Contains(Permissions.BillingRead) ? w.Invoices.Take(200).ToList() : [],
                Audit = p.Contains(Permissions.AuditRead) ? w.Audit.Take(200).ToList() : []
            }, w.Patients.Count > 200 || w.Appointments.Count > 200 || w.Encounters.Count > 200 || w.Invoices.Count > 200);
        }, ct);
    }
    public Task<IReadOnlyList<Patient>> FindDuplicates(Scope scope, PatientInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.PatientRead);
        return await s.FindDuplicatesAsync(input.Phone, input.FirstName, input.LastName, input.DateOfBirth, ct);
    }, ct);
    public Task<Patient> Register(Scope scope, PatientInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.PatientWrite);
        await RequireModule(s, "patients", "Patient registration is disabled.", ct);
        ClinicalRules.ValidatePatient(input.FirstName, input.LastName, input.DateOfBirth, input.Phone, input.Sex, scope.Today);
        ValidatePatientDetails(input);
        var duplicates = await s.FindDuplicatesAsync(input.Phone, input.FirstName, input.LastName, input.DateOfBirth, ct);
        if (duplicates.Count > 0 && input.DuplicateReviewReason.Trim().Length < 10)
            throw new DomainException("patient.duplicate", "A possible existing patient was found. Search the registry or record a duplicate-review reason.", 409);
        var uhid = await s.NextNumberAsync("UHID", scope.HospitalId.ToString(), $"NCH-{clock.UtcNow.Year}", ct);
        var patient = new Patient(Guid.NewGuid(), uhid, input.FirstName.Trim(), input.LastName.Trim(), input.DateOfBirth, input.Sex,
            input.Phone.Trim(), input.Email.Trim(), input.BloodGroup, input.Address.Trim(), input.EmergencyContact.Trim(), clock.UtcNow);
        await s.InsertPatientAsync(patient, ct);
        await s.AddAuditAsync(Event(scope, "patient.registered", "Patient", patient.Id, input.DuplicateReviewReason), ct);
        return patient;
    }, ct);
    public Task<Patient> UpdatePatient(Scope scope, Guid id, PatientInput input, long version, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.PatientWrite);
        var old = await s.FindPatientAsync(id, ct) ?? await Missing<Patient>("Patient");
        if (!old.Active) throw new DomainException("patient.inactive", "Inactive patient records cannot be edited.", 409);
        if (old.Version != version) Conflict();
        ClinicalRules.ValidatePatient(input.FirstName, input.LastName, input.DateOfBirth, input.Phone, input.Sex, scope.Today);
        ValidatePatientDetails(input);
        var duplicates = await s.FindDuplicatesAsync(input.Phone, input.FirstName, input.LastName, input.DateOfBirth, ct);
        if (duplicates.Any(p => p.Id != id) && input.DuplicateReviewReason.Trim().Length < 10)
            throw new DomainException("patient.duplicate", "A possible existing patient was found. Search the registry or record a duplicate-review reason.", 409);
        var patient = old with { FirstName = input.FirstName.Trim(), LastName = input.LastName.Trim(), DateOfBirth = input.DateOfBirth, Sex = input.Sex,
            Phone = input.Phone.Trim(), Email = input.Email.Trim(), Address = input.Address.Trim(), EmergencyContact = input.EmergencyContact.Trim(), BloodGroup = input.BloodGroup, Version = version + 1 };
        await s.UpdatePatientAsync(patient, ct);
        await s.AddAuditAsync(Event(scope, "patient.updated", "Patient", id, input.DuplicateReviewReason, new { Before = old, After = patient }), ct);
        return patient;
    }, ct);
    public Task<Chart> GetChart(Scope scope, Guid patientId, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.PatientRead);
        var chart = await s.LoadChartAsync(patientId, 200, ct) ?? await Missing<Chart>("Patient");
        await s.AddAuditAsync(Event(scope, "patient.chart.viewed", "Patient", patientId), ct);
        return chart;
    }, ct);
    public Task<Appointment> Book(Scope scope, BookingInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.ScheduleWrite);
        await RequireModule(s, "appointments", "Appointment creation is disabled.", ct);
        if (await s.FindPatientAsync(input.PatientId, ct) is not { Active: true } || !await s.HasActivePractitionerAsync(input.DoctorId, ct))
            throw new DomainException("booking.reference", "Select an active patient and a hospital practitioner.");
        if (input.StartsAt < clock.UtcNow.AddMinutes(-1) || input.StartsAt > clock.UtcNow.AddDays(180) || input.DurationMinutes is < 5 or > 120 || input.Reason.Length > 500)
            throw new DomainException("booking.slot", "Choose a future slot within 180 days, a duration of 5–120 minutes and a bounded reason.");
        if (!new[] { "In person", "Follow-up", "Telemedicine" }.Contains(input.Mode)) throw new DomainException("booking.mode", "Unsupported appointment type.");
        if (input.Mode == "Telemedicine") await RequireModule(s, "telemedicine", "Telemedicine is not enabled for this branch.", ct);
        if (await s.HasAppointmentConflictAsync(input.DoctorId, input.StartsAt, input.StartsAt.AddMinutes(input.DurationMinutes), null, ct))
            throw new DomainException("booking.conflict", "The practitioner already has an appointment in this time range.", 409);
        var appointment = new Appointment(Guid.NewGuid(), input.PatientId, input.DoctorId, input.StartsAt.ToUniversalTime(), input.DurationMinutes, input.Mode, input.Reason.Trim(), "Booked", null, clock.UtcNow);
        await s.InsertAppointmentAsync(appointment, ct);
        await s.AddAuditAsync(Event(scope, "appointment.booked", "Appointment", appointment.Id), ct);
        await s.AddOutboxAsync("appointment.booked", appointment.Id, JsonSerializer.Serialize(new { appointment.Id, PatientId = appointment.PatientId }), ct);
        return appointment;
    }, ct);
    public Task<Appointment> Transition(Scope scope, Guid id, string action, long version, string reason, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.ScheduleWrite);
        var a = await s.FindAppointmentAsync(id, ct) ?? await Missing<Appointment>("Appointment");
        if (a.Version != version) Conflict();
        var next = (a.Status, action) switch
        {
            ("Booked", "check-in") => "Checked in",
            ("Checked in", "start") => "In consultation",
            ("In consultation", "complete") => "Completed",
            ("Booked" or "Checked in", "cancel") => "Cancelled",
            _ => throw new DomainException("appointment.transition", "This queue transition is not allowed.", 409)
        };
        if (action == "cancel" && reason.Trim().Length < 5) throw new DomainException("appointment.reason", "Provide a cancellation reason.");
        if (action == "complete" && await s.FindEncounterByAppointmentAsync(id, ct) is not { Status: "Signed" })
            throw new DomainException("appointment.unsigned", "Sign the consultation before completing the visit.");
        var token = a.Token;
        if (action == "check-in")
        {
            var key = $"{scope.BranchId}:{a.DoctorId}:{DateOnly.FromDateTime(a.StartsAt.UtcDateTime):yyyyMMdd}";
            token = int.Parse((await s.NextNumberAsync("TOKEN", key, "", ct)).Split('-')[^1]);
        }
        a = a with { Status = next, Token = token, Version = version + 1 };
        await s.UpdateAppointmentAsync(a, ct);
        await s.AddAuditAsync(Event(scope, $"appointment.{action}", "Appointment", id, reason), ct);
        return a;
    }, ct);
    public Task<Appointment> Reschedule(Scope scope, Guid id, DateTimeOffset startsAt, long version, string reason, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.ScheduleWrite);
        await RequireModule(s, "appointments", "Appointment changes are disabled.", ct);
        var old = await s.FindAppointmentAsync(id, ct) ?? await Missing<Appointment>("Appointment");
        if (old.Version != version) Conflict();
        if (old.Status != "Booked" || reason.Length < 5) throw new DomainException("appointment.reschedule", "Only booked appointments can be rescheduled; provide a reason.");
        if (startsAt < clock.UtcNow || startsAt > clock.UtcNow.AddDays(180) || await s.HasAppointmentConflictAsync(old.DoctorId, startsAt, startsAt.AddMinutes(old.DurationMinutes), id, ct))
            throw new DomainException("booking.conflict", "The new practitioner slot is unavailable.");
        var updated = old with { StartsAt = startsAt.ToUniversalTime(), Version = version + 1 };
        await s.UpdateAppointmentAsync(updated, ct);
        await s.AddAuditAsync(Event(scope, "appointment.rescheduled", "Appointment", id, reason, new { Before = old.StartsAt, After = updated.StartsAt }), ct);
        return updated;
    }, ct);
    public Task<Encounter> StartEncounter(Scope scope, Guid appointmentId, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.ClinicalWrite); scope.Require(Permissions.ClinicalSign);
        await RequireModule(s, "consultations", "New clinical work is disabled for this branch.", ct);
        var a = await s.FindAppointmentAsync(appointmentId, ct) ?? await Missing<Appointment>("Appointment");
        var existing = await s.FindEncounterByAppointmentAsync(appointmentId, ct);
        if (existing is not null) return existing;
        if (a.DoctorId != scope.Actor.Id || !new[] { "Checked in", "In consultation" }.Contains(a.Status))
            throw new DomainException("encounter.assignment", "Only the assigned doctor can start a checked-in consultation.", 403);
        var e = NewEncounter(a.PatientId, a.Id, a.DoctorId, clock.UtcNow);
        await s.InsertEncounterAsync(e, ct);
        await s.UpdateAppointmentAsync(a with { Status = "In consultation", Version = a.Version + 1 }, ct);
        await s.AddAuditAsync(Event(scope, "encounter.started", "Encounter", e.Id), ct);
        return e;
    }, ct);
    private static Encounter NewEncounter(Guid patientId, Guid? appointmentId, Guid doctorId, DateTimeOffset now) =>
        new(Guid.NewGuid(), patientId, appointmentId, doctorId, "Draft", "", "", "", "", "", new(null, null, null, null, null, null), [], [], [], now);
    public Task<Encounter> SaveDraft(Scope scope, Guid id, DraftInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.ClinicalWrite); scope.Require(Permissions.ClinicalSign);
        var medicines = await s.LoadMedicinesAsync(ct);
        var e = await s.FindEncounterAsync(id, ct) ?? await Missing<Encounter>("Encounter");
        if (e.DoctorId != scope.Actor.Id) throw new DomainException("encounter.assignment", "Only the assigned doctor can edit this record.", 403);
        if (e.Status != "Draft" || e.Version != input.Version) Conflict();
        if (new[] { input.ChiefComplaint, input.Examination, input.Diagnosis, input.Plan, input.FollowUp }.Any(x => x.Length > 10000) || input.Allergies.Count > 100 || input.Prescription.Count > 50 || input.Orders.Count > 100)
            throw new DomainException("encounter.size", "The clinical record exceeds the permitted field or collection size.");
        if (input.Allergies.Any(a => a.Substance.Length is 0 or > 200 || a.Reaction.Length > 500 || a.Severity.Length > 40) || input.Orders.Any(o => !new[] { "Laboratory", "Radiology", "Procedure" }.Contains(o.Kind) || o.Name.Length is 0 or > 200 || o.Instructions.Length > 1000))
            throw new DomainException("encounter.fields", "Review allergy and order fields.");
        ClinicalRules.ValidateVitals(input.Vitals);
        var prescription = input.Prescription.Select(item => { var medicine = medicines.FirstOrDefault(m => m.Id == item.MedicineId); return item with { Name = medicine is null ? item.Name : $"{medicine.GenericName} {medicine.Strength}" }; }).ToList();
        ClinicalRules.ValidatePrescription(prescription, input.Allergies, medicines.Select(m => m.Id).ToHashSet());
        var updated = e with { ChiefComplaint = input.ChiefComplaint, Examination = input.Examination, Diagnosis = input.Diagnosis, Plan = input.Plan, FollowUp = input.FollowUp,
            Vitals = input.Vitals, Allergies = input.Allergies, Prescription = prescription, Orders = input.Orders, AllergyReviewed = input.AllergyReviewed, UpdatedAt = clock.UtcNow, Version = e.Version + 1 };
        await s.UpdateEncounterAsync(updated, ct);
        await s.AddAuditAsync(Event(scope, "encounter.draft.saved", "Encounter", id), ct);
        return updated;
    }, ct);
    public Task<Encounter> Sign(Scope scope, Guid id, long version, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.ClinicalWrite); scope.Require(Permissions.ClinicalSign);
        var medicines = await s.LoadMedicinesAsync(ct);
        var e = await s.FindEncounterAsync(id, ct) ?? await Missing<Encounter>("Encounter");
        if (e.DoctorId != scope.Actor.Id) throw new DomainException("encounter.assignment", "Only the assigned doctor can sign.", 403);
        if (e.Status != "Draft" || e.Version != version) Conflict();
        if (string.IsNullOrWhiteSpace(e.ChiefComplaint) || string.IsNullOrWhiteSpace(e.Diagnosis) || !e.AllergyReviewed)
            throw new DomainException("encounter.sign", "Chief complaint, diagnosis and explicit allergy reconciliation are required before signing.");
        ClinicalRules.ValidatePrescription(e.Prescription, e.Allergies, medicines.Select(m => m.Id).ToHashSet());
        var signedAt = clock.UtcNow;
        var hash = CanonicalHash(e);
        var signed = e with { Status = "Signed", SignedAt = signedAt, SignedBy = scope.Actor.Name, ContentHash = hash, Version = version + 1, UpdatedAt = signedAt };
        await s.UpdateEncounterAsync(signed, ct);
        await s.AddAuditAsync(Event(scope, "encounter.signed", "Encounter", id, changes: new { Hash = hash, Version = signed.Version }), ct);
        await s.AddOutboxAsync("encounter.signed", id, JsonSerializer.Serialize(new { EncounterId = id, Hash = hash }), ct);
        return signed;
    }, ct);
    public static string CanonicalHash(Encounter e)
    {
        // Version-independent canonical clinical content. Ordering of collections is clinically significant.
        var content = JsonSerializer.Serialize(new { SchemaVersion = 1, e.PatientId, e.AppointmentId, e.DoctorId, e.ChiefComplaint, e.Examination, e.Diagnosis, e.Plan, e.FollowUp, e.Vitals, e.Allergies, e.Prescription, e.Orders, e.AllergyReviewed });
        return Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
    }
    public Task<Invoice> CreateInvoice(Scope scope, InvoiceInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.BillingWrite);
        await RequireModule(s, "billing", "Billing is disabled for this branch.", ct);
        var services = await s.LoadServicesAsync(ct);
        if (await s.FindPatientAsync(input.PatientId, ct) is not { Active: true } || input.Lines.Count is 0 or > 100) throw new DomainException("invoice.lines", "Select a patient and 1–100 services.");
        if (input.EncounterId.HasValue) { var linked = await s.FindEncounterAsync(input.EncounterId.Value, ct); if (linked is null || linked.PatientId != input.PatientId) throw new DomainException("invoice.encounter", "The encounter does not belong to this patient."); }
        var lines = input.Lines.Select(l =>
        {
            var service = services.FirstOrDefault(x => x.Id == l.ServiceId) ?? throw new DomainException("invoice.service", "Unknown service.");
            if (l.Quantity is < 1 or > 100) throw new DomainException("invoice.quantity", "Quantity must be between 1 and 100.");
            return new InvoiceLine(service.Id, service.Name, l.Quantity, service.Price);
        }).ToList();
        var number = await s.NextNumberAsync("INVOICE", $"{scope.BranchId}:{scope.Today:yyyy}", $"INV-{scope.Today:yyyy}", ct);
        var invoice = new Invoice(Guid.NewGuid(), number, input.PatientId, input.EncounterId, "Draft", lines, 0, 0, [], clock.UtcNow);
        await s.InsertInvoiceAsync(invoice, ct);
        await s.AddAuditAsync(Event(scope, "invoice.created", "Invoice", invoice.Id, changes: new { Lines = lines, Total = invoice.Total }), ct);
        return invoice;
    }, ct);
    public Task<Invoice> IssueInvoice(Scope scope, Guid id, long version, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.BillingWrite);
        var invoice = await s.FindInvoiceAsync(id, ct) ?? await Missing<Invoice>("Invoice");
        if (invoice.Version != version) Conflict();
        if (invoice.Status != "Draft" || invoice.Total <= 0) throw new DomainException("invoice.issue", "Only a positive-value draft invoice can be issued.");
        invoice = invoice with { Status = "Issued", IssuedAt = clock.UtcNow, Version = version + 1 };
        await s.UpdateInvoiceAsync(invoice, ct);
        await s.AddAuditAsync(Event(scope, "invoice.issued", "Invoice", id, changes: new { invoice.Total }), ct);
        return invoice;
    }, ct);
    public Task<Invoice> Pay(Scope scope, Guid id, PaymentInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.BillingCollect);
        var invoice = await s.FindInvoiceAsync(id, ct) ?? await Missing<Invoice>("Invoice");
        input = input with { Reference = input.Reference.Trim() };
        var duplicate = invoice.Payments.FirstOrDefault(p => p.Method == input.Method && p.Reference == input.Reference);
        if (duplicate is not null)
        {
            if (duplicate.Amount != input.Amount) throw new DomainException("payment.idempotency", "This payment reference was already used for a different amount.", 409);
            return invoice;
        }
        if (invoice.Version != input.Version) Conflict();
        if (!new[] { "Issued", "Part paid" }.Contains(invoice.Status)) throw new DomainException("payment.state", "Payments can only be recorded against an issued, outstanding invoice.");
        ClinicalRules.ValidatePayment(input.Amount, invoice.Balance);
        if (!new[] { "Cash", "Card", "UPI", "Bank transfer" }.Contains(input.Method) || string.IsNullOrWhiteSpace(input.Reference) || input.Reference.Length > 100)
            throw new DomainException("payment.reference", "Select a payment method and enter a unique payment reference.");
        invoice = invoice with { Payments = [.. invoice.Payments, new Payment(Guid.NewGuid(), input.Amount, input.Method, input.Reference, clock.UtcNow, scope.Actor.Id)],
            Status = invoice.Balance - input.Amount == 0 ? "Paid" : "Part paid", Version = input.Version + 1 };
        await s.UpdateInvoiceAsync(invoice, ct);
        await s.AddAuditAsync(Event(scope, "invoice.payment.recorded", "Invoice", id, changes: new { input.Amount, input.Method, input.Reference }), ct);
        return invoice;
    }, ct);
    public Task<ModuleSetting> SetModule(Scope scope, string key, bool enabled, string reason, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.Admin);
        var module = await s.FindModuleAsync(key, ct) ?? await Missing<ModuleSetting>("Module");
        if (module.Phase > 1 && enabled) throw new DomainException("module.unavailable", "This module has not passed its implementation and release gates.");
        if (reason.Trim().Length < 10) throw new DomainException("policy.reason", "Record a configuration-change reason of at least 10 characters.");
        await s.SetModuleAsync(key, enabled, ct);
        await s.AddAuditAsync(Event(scope, "module.configured", "Module", Guid.NewGuid(), reason, new { Key = key, Before = module.Enabled, After = enabled }), ct);
        return module with { Enabled = enabled };
    }, ct);
    public async Task<AuthResult> Login(LoginInput input, Guid hospitalId, Guid branchId, CancellationToken ct, string? sourceIp = null)
    {
        if (input.UserName.Length > 100 || input.Password.Length > 256) throw new DomainException("auth.input", "Invalid login credentials.", 401);
        var normalized = input.UserName.Trim().ToLowerInvariant();
        var result = await unitOfWork.ExecuteAsync<AuthResult?>(new Scope(hospitalId, branchId, new Principal(Guid.Empty, "Anonymous", "Anonymous", hospitalId, branchId), DateOnly.FromDateTime(clock.UtcNow.UtcDateTime)), async s =>
        {
            var user = await s.FindUserAsync(normalized, branchId, ct);
            var valid = passwords.Verify(user?.PasswordHash ?? dummyHash, input.Password, out var needsRehash) && user is not null && user.Enabled && user.HospitalId == hospitalId;
            await s.RecordLoginAsync(user?.Id, normalized, valid, sourceIp, ct);
            if (!valid || user is null) return null;
            if (needsRehash) await s.UpdatePasswordAsync(user.Id, passwords.Hash(input.Password), ct);
            var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            var expiry = clock.UtcNow.AddHours(8);
            await s.CreateSessionAsync(new UserSession(Guid.NewGuid(), user.Id, TokenHash(token), expiry), ct);
            return new AuthResult(new Principal(user.Id, user.DisplayName, user.Role, user.HospitalId, user.BranchId) { Permissions = user.Grants ?? Careflow.Domain.Permissions.For(user.Role) }, token, expiry);
        }, ct);
        return result ?? throw new DomainException("auth.failed", "Invalid username or password.", 401);
    }
    public Task<Principal?> Authenticate(string token, Guid hospitalId, Guid branchId, CancellationToken ct) => unitOfWork.ExecuteAsync(new Scope(hospitalId, branchId, new Principal(Guid.Empty, "Anonymous", "Anonymous", hospitalId, branchId), DateOnly.FromDateTime(clock.UtcNow.UtcDateTime)), async s =>
    {
        var session = await s.FindSessionAsync(TokenHash(token), ct);
        if (session is null || session.ExpiresAt <= clock.UtcNow || session.RevokedAt is not null) return null;
        var user = await s.FindUserAsync(session.UserId, hospitalId, branchId, ct);
        return user is null || !user.Enabled ? null : new Principal(user.Id, user.DisplayName, user.Role, user.HospitalId, user.BranchId) { Permissions = user.Grants ?? Careflow.Domain.Permissions.For(user.Role) };
    }, ct);
    public Task Logout(string token, Guid hospitalId, Guid branchId, CancellationToken ct) => unitOfWork.ExecuteAsync(new Scope(hospitalId, branchId, new Principal(Guid.Empty, "Anonymous", "Anonymous", hospitalId, branchId), DateOnly.FromDateTime(clock.UtcNow.UtcDateTime)), async s =>
    {
        var session = await s.FindSessionAsync(TokenHash(token), ct);
        if (session is not null) { await s.RevokeSessionAsync(session.Id, ct); await s.AddAuditAsync(new AuditEvent(Guid.NewGuid(), session.UserId, "Session", "auth.logout", "Session", session.Id, "", clock.UtcNow), ct); }
        return true;
    }, ct);
    public static string TokenHash(string token) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
    private AuditEvent Event(Scope scope, string action, string entity, Guid id, string reason = "", object? changes = null) =>
        new(Guid.NewGuid(), scope.Actor.Id, scope.Actor.Name, action, entity, id, reason, clock.UtcNow, changes is null ? null : JsonSerializer.Serialize(changes));
}
