using Careflow.Domain;
namespace Careflow.Application;

public sealed record Scope(Guid HospitalId, Guid BranchId, Principal Actor, DateOnly Today)
{
    public static Scope For(Principal actor, DateTimeOffset now) => new(actor.HospitalId, actor.BranchId, actor, DateOnly.FromDateTime(now.UtcDateTime));
    public void Require(string permission)
    {
        if (!Actor.Has(permission)) throw new DomainException("forbidden", "Your role does not permit this action.", 403);
    }
}
public sealed record PatientInput(string FirstName, string LastName, DateOnly DateOfBirth, string Sex,
    string Phone = "", string Email = "", string BloodGroup = "Unknown", string Address = "", string EmergencyContact = "", string DuplicateReviewReason = "");
public sealed record BookingInput(Guid PatientId, Guid DoctorId, DateTimeOffset StartsAt, int DurationMinutes = 20, string Mode = "In person", string Reason = "");
public sealed record DraftInput(string ChiefComplaint, string Examination, string Diagnosis, string Plan, string FollowUp,
    VitalSet Vitals, IReadOnlyList<Allergy> Allergies, IReadOnlyList<PrescriptionItem> Prescription,
    IReadOnlyList<ClinicalOrder> Orders, bool AllergyReviewed, long Version);
public sealed record InvoiceInput(Guid PatientId, IReadOnlyList<InvoiceLineInput> Lines, Guid? EncounterId = null);
public sealed record InvoiceLineInput(Guid ServiceId, int Quantity);
public sealed record PaymentInput(decimal Amount, string Method, string Reference, long Version);
public sealed record VersionInput(long Version, string Reason = "");
public sealed record RescheduleInput(long Version, DateTimeOffset StartsAt, string Reason);
public sealed record ModuleInput(bool Enabled, string Reason);
public sealed record LoginInput(string UserName, string Password);
public sealed record UserAccount(Guid Id, string UserName, string DisplayName, string PasswordHash, string Role, Guid HospitalId, Guid BranchId, bool Enabled)
{
    public IReadOnlyList<string>? Grants { get; init; }
}
public sealed record UserSession(Guid Id, Guid UserId, string TokenHash, DateTimeOffset ExpiresAt, DateTimeOffset? RevokedAt = null);
public sealed record AuthResult(Principal Principal, string Token, DateTimeOffset ExpiresAt);
public sealed record WorkspaceResult(Workspace Workspace, bool Truncated);

public interface IHospitalStore
{
    Task<Workspace> LoadAsync(Scope scope, int limit, CancellationToken ct);
    Task<Patient?> FindPatientAsync(Guid id, CancellationToken ct);
    Task<Appointment?> FindAppointmentAsync(Guid id, CancellationToken ct);
    Task<Encounter?> FindEncounterAsync(Guid id, CancellationToken ct);
    Task<Encounter?> FindEncounterByAppointmentAsync(Guid appointmentId, CancellationToken ct);
    Task<Invoice?> FindInvoiceAsync(Guid id, CancellationToken ct);
    Task<ModuleSetting?> FindModuleAsync(string key, CancellationToken ct);
    Task<bool> HasActivePractitionerAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Medicine>> LoadMedicinesAsync(CancellationToken ct);
    Task<IReadOnlyList<ServiceItem>> LoadServicesAsync(CancellationToken ct);
    Task<bool> HasAppointmentConflictAsync(Guid doctorId, DateTimeOffset start, DateTimeOffset end, Guid? excludeId, CancellationToken ct);
    Task<Chart?> LoadChartAsync(Guid patientId, int limit, CancellationToken ct);
    Task<IReadOnlyList<Patient>> FindDuplicatesAsync(string phone, string firstName, string lastName, DateOnly dob, CancellationToken ct);
    Task<string> NextNumberAsync(string kind, string scope, string prefix, CancellationToken ct);
    Task InsertPatientAsync(Patient patient, CancellationToken ct);
    Task UpdatePatientAsync(Patient patient, CancellationToken ct);
    Task InsertAppointmentAsync(Appointment appointment, CancellationToken ct);
    Task UpdateAppointmentAsync(Appointment appointment, CancellationToken ct);
    Task InsertEncounterAsync(Encounter encounter, CancellationToken ct);
    Task UpdateEncounterAsync(Encounter encounter, CancellationToken ct);
    Task InsertInvoiceAsync(Invoice invoice, CancellationToken ct);
    Task UpdateInvoiceAsync(Invoice invoice, CancellationToken ct);
    Task SetModuleAsync(string key, bool enabled, CancellationToken ct);
    Task AddAuditAsync(AuditEvent audit, CancellationToken ct);
    Task AddOutboxAsync(string type, Guid aggregateId, string payload, CancellationToken ct);
    Task<UserAccount?> FindUserAsync(string userName, Guid branchId, CancellationToken ct);
    Task<UserSession?> FindSessionAsync(string tokenHash, CancellationToken ct);
    Task<UserAccount?> FindUserAsync(Guid id, Guid hospitalId, Guid branchId, CancellationToken ct);
    Task CreateSessionAsync(UserSession session, CancellationToken ct);
    Task RevokeSessionAsync(Guid id, CancellationToken ct);
    Task UpdatePasswordAsync(Guid userId, string hash, CancellationToken ct);
    Task RecordLoginAsync(Guid? userId, string userName, bool success, string? sourceIp, CancellationToken ct);
}
public interface IUnitOfWork
{
    Task<T> ExecuteAsync<T>(Scope scope, Func<IHospitalStore, Task<T>> action, CancellationToken ct);
}
public interface IPasswordService
{
    string Hash(string password);
    bool Verify(string hash, string password, out bool needsRehash);
}
public interface IClock { DateTimeOffset UtcNow { get; } }
public sealed class SystemClock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }
