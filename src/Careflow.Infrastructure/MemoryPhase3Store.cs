using Careflow.Application;
using Careflow.Domain;
namespace Careflow.Infrastructure;

public sealed partial class MemoryHospitalStore
{
    public Task<RadiologyOrder?> FindRadiologyOrderAsync(Guid id, CancellationToken ct) => Task.FromResult(data.RadiologyOrders.FirstOrDefault(o => o.Id == id));
    public Task InsertRadiologyOrderAsync(RadiologyOrder order, CancellationToken ct) { data.RadiologyOrders.Add(order); return Task.CompletedTask; }
    public Task UpdateRadiologyOrderAsync(RadiologyOrder order, CancellationToken ct) { var old = FindRadiologyOrderAsync(order.Id, ct).Result ?? throw new DomainException("not_found", "Radiology order not found.", 404); CareVersion(old.Version, order.Version); if (old.Status is "Authorized" or "Cancelled") throw new DomainException("radiology.immutable", "Authorized or cancelled studies are immutable.", 409); data.RadiologyOrders[data.RadiologyOrders.IndexOf(old)] = order; return Task.CompletedTask; }
    public Task<RadiologyReport?> FindRadiologyReportByOrderAsync(Guid orderId, CancellationToken ct) => Task.FromResult(data.RadiologyReports.FirstOrDefault(r => r.OrderId == orderId));
    public Task InsertRadiologyReportAsync(RadiologyReport report, CancellationToken ct) { data.RadiologyReports.Add(report); return Task.CompletedTask; }
    public Task UpdateRadiologyReportAsync(RadiologyReport report, CancellationToken ct) { var old = data.RadiologyReports.FirstOrDefault(r => r.Id == report.Id) ?? throw new DomainException("not_found", "Radiology report not found.", 404); if (old.Status == "Authorized") throw new DomainException("radiology.immutable", "Authorized reports are immutable.", 409); data.RadiologyReports[data.RadiologyReports.IndexOf(old)] = report; return Task.CompletedTask; }
    public Task<EmergencyRegistration?> FindEmergencyRegistrationAsync(Guid id, CancellationToken ct) => Task.FromResult(data.EmergencyRegistrations.FirstOrDefault(r => r.Id == id));
    public Task InsertEmergencyRegistrationAsync(EmergencyRegistration registration, CancellationToken ct) { data.EmergencyRegistrations.Add(registration); return Task.CompletedTask; }
    public Task UpdateEmergencyRegistrationAsync(EmergencyRegistration registration, CancellationToken ct) { var old = FindEmergencyRegistrationAsync(registration.Id, ct).Result ?? throw new DomainException("not_found", "Emergency registration not found.", 404); CareVersion(old.Version, registration.Version); if (old.Status != "Open") throw new DomainException("emergency.immutable", "Closed emergency episodes are immutable.", 409); data.EmergencyRegistrations[data.EmergencyRegistrations.IndexOf(old)] = registration; return Task.CompletedTask; }
    public Task InsertTriageAssessmentAsync(TriageAssessment assessment, CancellationToken ct) { data.TriageAssessments.Add(assessment); return Task.CompletedTask; }
    public Task InsertEmergencyObservationAsync(EmergencyObservation observation, CancellationToken ct) { data.EmergencyObservations.Add(observation); return Task.CompletedTask; }
    public Task InsertEmergencyDispositionAsync(EmergencyDisposition disposition, CancellationToken ct) { data.EmergencyDispositions.Add(disposition); return Task.CompletedTask; }
    public Task<OtBooking?> FindOtBookingAsync(Guid id, CancellationToken ct) => Task.FromResult(data.OtBookings.FirstOrDefault(b => b.Id == id));
    public Task InsertOtBookingAsync(OtBooking booking, CancellationToken ct) { data.OtBookings.Add(booking); return Task.CompletedTask; }
    public Task UpdateOtBookingAsync(OtBooking booking, CancellationToken ct) { var old = FindOtBookingAsync(booking.Id, ct).Result ?? throw new DomainException("not_found", "Theatre booking not found.", 404); CareVersion(old.Version, booking.Version); if (old.Status is "Completed" or "Cancelled") throw new DomainException("ot.immutable", "Completed or cancelled surgeries are immutable.", 409); data.OtBookings[data.OtBookings.IndexOf(old)] = booking; return Task.CompletedTask; }
    public Task<OtChecklist?> FindOtChecklistAsync(Guid bookingId, CancellationToken ct) => Task.FromResult(data.OtChecklists.FirstOrDefault(c => c.BookingId == bookingId));
    public Task InsertOtChecklistAsync(OtChecklist checklist, CancellationToken ct) { data.OtChecklists.Add(checklist); return Task.CompletedTask; }
    public Task<bool> HasOtConflictAsync(string theatre, DateTimeOffset start, DateTimeOffset end, Guid? excludeId, CancellationToken ct) => Task.FromResult(data.OtBookings.Any(b => b.Theatre == theatre && b.Status is "Scheduled" or "In Theatre" or "Recovery" && b.ScheduledStart < end && b.ScheduledEnd > start && b.Id != excludeId));
    public Task<BloodUnit?> FindBloodUnitAsync(Guid id, CancellationToken ct) => Task.FromResult(data.BloodUnits.FirstOrDefault(u => u.Id == id));
    public Task<BloodUnit?> FindBloodUnitByNumberAsync(string unitNumber, CancellationToken ct) => Task.FromResult(data.BloodUnits.FirstOrDefault(u => u.UnitNumber == unitNumber));
    public Task InsertBloodUnitAsync(BloodUnit unit, CancellationToken ct) { data.BloodUnits.Add(unit); return Task.CompletedTask; }
    public Task UpdateBloodUnitAsync(BloodUnit unit, CancellationToken ct) { var old = FindBloodUnitAsync(unit.Id, ct).Result ?? throw new DomainException("not_found", "Blood unit not found.", 404); CareVersion(old.Version, unit.Version); if (old.Status is "Transfused" or "Discarded") throw new DomainException("blood.immutable", "Transfused or discarded units are immutable.", 409); data.BloodUnits[data.BloodUnits.IndexOf(old)] = unit; return Task.CompletedTask; }
    public Task<BloodScreening?> FindScreeningAsync(Guid unitId, CancellationToken ct) => Task.FromResult(data.BloodScreenings.FirstOrDefault(s => s.UnitId == unitId));
    public Task InsertScreeningAsync(BloodScreening screening, CancellationToken ct) { data.BloodScreenings.Add(screening); return Task.CompletedTask; }
    public Task<Crossmatch?> FindActiveCrossmatchAsync(Guid unitId, Guid patientId, CancellationToken ct) => Task.FromResult(data.Crossmatches.FirstOrDefault(x => x.UnitId == unitId && x.PatientId == patientId && x.Status == "Matched"));
    public Task InsertCrossmatchAsync(Crossmatch crossmatch, CancellationToken ct) { data.Crossmatches.Add(crossmatch); return Task.CompletedTask; }
    public Task<BloodIssue?> FindBloodIssueAsync(Guid id, CancellationToken ct) => Task.FromResult(data.BloodIssues.FirstOrDefault(i => i.Id == id));
    public Task<BloodIssue?> FindOpenBloodIssueAsync(Guid unitId, CancellationToken ct) => Task.FromResult(data.BloodIssues.FirstOrDefault(i => i.UnitId == unitId && i.Status == "Issued"));
    public Task InsertBloodIssueAsync(BloodIssue issue, CancellationToken ct) { data.BloodIssues.Add(issue); return Task.CompletedTask; }
    public Task UpdateBloodIssueAsync(BloodIssue issue, CancellationToken ct) { var old = FindBloodIssueAsync(issue.Id, ct).Result ?? throw new DomainException("not_found", "Blood issue not found.", 404); CareVersion(old.Version, issue.Version); if (old.Status is "Transfused" or "Returned") throw new DomainException("blood.immutable", "Closed blood issues are immutable.", 409); data.BloodIssues[data.BloodIssues.IndexOf(old)] = issue; return Task.CompletedTask; }
}
