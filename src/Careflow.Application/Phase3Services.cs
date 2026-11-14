using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Careflow.Domain;
namespace Careflow.Application;

internal static class Phase3Rules
{
    public static readonly IReadOnlyList<string> Modalities = ["X-ray", "Ultrasound", "CT", "MRI"];
    public static readonly IReadOnlyList<string> Priorities = ["Routine", "Urgent", "STAT"];
    public static readonly IReadOnlyList<string> Laterality = ["Left", "Right", "Bilateral", "Not applicable"];
    public static readonly IReadOnlyList<string> PairedSites = ["Knee", "Ankle", "Wrist", "Elbow", "Shoulder", "Hip", "Eye", "Ear", "Kidney", "Lung", "Breast", "Hand", "Foot", "Arm", "Leg"];
    public static readonly IReadOnlyList<string> TriageCategories = ["Red", "Yellow", "Green", "Black"];
    public static readonly IReadOnlyList<string> ArrivalModes = ["Walk-in", "Ambulance", "Police", "Referral", "Other"];
    public static readonly IReadOnlyList<string> DispositionKinds = ["Admit", "Discharge", "Refer", "LAMA"];
    public static readonly IReadOnlyList<string> Anaesthetics = ["General", "Regional", "Local", "Sedation"];
    public static readonly IReadOnlyList<string> OtPriorities = ["Elective", "Urgent", "Emergency"];
    public static void ValidateLaterality(string site, string laterality)
    {
        if (!Laterality.Contains(laterality)) throw new DomainException("care.fields", "Select a valid laterality.");
        if (PairedSites.Any(p => site.Contains(p, StringComparison.OrdinalIgnoreCase)) && laterality == "Not applicable")
            throw new DomainException("phase3.laterality", "Laterality is required for paired anatomical sites.", 422);
    }
    public static string ReportHash(RadiologyOrder order, RadiologyReport report) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        JsonSerializer.Serialize(new { SchemaVersion = 2, order.Number, order.Accession, order.PatientId, order.Procedure, order.Modality, order.BodyPart, order.Laterality, report.Findings, report.Impression, report.Critical, report.Version })))).ToLowerInvariant();
}

public sealed class RadiologyService(IUnitOfWork unitOfWork, IClock clock)
{
    public Task<RadiologyOrder> Order(Scope scope, RadiologyOrderInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.RadiologyOrder); await CareGuard.Module(s, "radiology", ct);
        if (await s.FindPatientAsync(input.PatientId, ct) is not { Active: true }) throw new DomainException("radiology.reference", "Select an active patient.");
        if (input.EncounterId is Guid encounterId && await s.FindEncounterAsync(encounterId, ct) is null) throw new DomainException("radiology.reference", "Linked encounter not found.");
        if (input.AdmissionId is Guid admissionId && await s.FindAdmissionAsync(admissionId, ct) is not { Status: "Active" }) throw new DomainException("radiology.reference", "Linked admission must be active.");
        var procedure = CareGuard.Text(input.Procedure, 3, 200, "Procedure");
        if (!Phase3Rules.Modalities.Contains(input.Modality)) throw new DomainException("radiology.modality", "Select a supported modality.");
        var bodyPart = CareGuard.Text(input.BodyPart, 2, 60, "Body part");
        Phase3Rules.ValidateLaterality(bodyPart, input.Laterality);
        if (!Phase3Rules.Priorities.Contains(input.Priority)) throw new DomainException("radiology.priority", "Select a valid priority.");
        var note = CareGuard.Text(input.ClinicalNote, 5, 1000, "Clinical indication");
        var number = await s.NextNumberAsync("RIS", $"{scope.BranchId}:{clock.UtcNow.Year}", $"RIS-{clock.UtcNow.Year}", ct);
        var order = new RadiologyOrder(Guid.NewGuid(), number, input.PatientId, input.EncounterId, input.AdmissionId,
            procedure, input.Modality, bodyPart, input.Laterality, input.Priority, "Ordered", null, note, scope.Actor.Id, clock.UtcNow, null, null, null);
        await s.InsertRadiologyOrderAsync(order, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "radiology.order.created", "RadiologyOrder", order.Id, procedure, new { input.Modality, input.BodyPart, input.Laterality, input.Priority }), ct);
        return order;
    }, ct);
    public Task<RadiologyOrder> Schedule(Scope scope, Guid id, RadiologyScheduleInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.RadiologySchedule); await CareGuard.Module(s, "radiology", ct);
        var order = await s.FindRadiologyOrderAsync(id, ct) ?? throw new DomainException("not_found", "Radiology order not found.", 404);
        CareGuard.Version(order.Version, input.Version);
        if (order.Status != "Ordered") throw new DomainException("radiology.state", "Only ordered studies can be scheduled.", 409);
        if (input.At < clock.UtcNow.AddMinutes(-5) || input.At > clock.UtcNow.AddDays(30)) throw new DomainException("radiology.schedule", "Schedule the study between now and the next 30 days.");
        var accession = await s.NextNumberAsync("RIS_ACCESSION", scope.BranchId.ToString(), "ACC-R", ct);
        var updated = order with { Status = "Scheduled", ScheduledAt = input.At, Accession = accession, Version = order.Version + 1 };
        await s.UpdateRadiologyOrderAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "radiology.order.scheduled", "RadiologyOrder", id, reason: accession, changes: new { input.At }), ct);
        return updated;
    }, ct);
    public Task<RadiologyOrder> Perform(Scope scope, Guid id, RadiologyPerformInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.RadiologyPerform); await CareGuard.Module(s, "radiology", ct);
        var order = await s.FindRadiologyOrderAsync(id, ct) ?? throw new DomainException("not_found", "Radiology order not found.", 404);
        CareGuard.Version(order.Version, input.Version);
        if (order.Status != "Scheduled") throw new DomainException("radiology.state", "Only scheduled studies can be performed.", 409);
        var uid = input.StudyUid.Trim();
        if (uid.Length is < 10 or > 64 || !uid.StartsWith("1.") || uid.Any(c => !(char.IsDigit(c) || c == '.'))) throw new DomainException("radiology.uid", "Enter a valid DICOM Study Instance UID (digits and dots, starting 1.).");
        var updated = order with { Status = "Performed", PerformedAt = clock.UtcNow, StudyUid = uid, Version = order.Version + 1 };
        await s.UpdateRadiologyOrderAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "radiology.study.performed", "RadiologyOrder", id, changes: new { StudyUid = uid }), ct);
        return updated;
    }, ct);
    public Task<RadiologyReport> Report(Scope scope, Guid id, RadiologyReportInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.RadiologyReport); await CareGuard.Module(s, "radiology", ct);
        var order = await s.FindRadiologyOrderAsync(id, ct) ?? throw new DomainException("not_found", "Radiology order not found.", 404);
        CareGuard.Version(order.Version, input.Version);
        if (order.Status != "Performed") throw new DomainException("radiology.state", "Reporting requires a performed study.", 409);
        if (await s.FindRadiologyReportByOrderAsync(id, ct) is not null) throw new DomainException("radiology.report.exists", "A report already exists for this study.", 409);
        var findings = CareGuard.Text(input.Findings, 10, 8000, "Findings");
        var impression = CareGuard.Text(input.Impression, 5, 2000, "Impression");
        var report = new RadiologyReport(Guid.NewGuid(), id, 1, findings, impression, input.Critical, "Reported", scope.Actor.Id, clock.UtcNow, null, null, null);
        await s.InsertRadiologyReportAsync(report, ct);
        var updated = order with { Status = "Reported", Version = order.Version + 1 };
        await s.UpdateRadiologyOrderAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "radiology.report.recorded", "RadiologyReport", report.Id, changes: new { input.Critical }), ct);
        if (input.Critical)
        {
            var alert = new CriticalAlert(Guid.NewGuid(), "Radiology", id, order.PatientId, $"Critical imaging finding · {order.Procedure}: {impression}", "Critical", "Open", clock.UtcNow, null, null, "");
            await s.InsertCriticalAlertAsync(alert, ct);
            await s.AddOutboxAsync("radiology.critical", id, JsonSerializer.Serialize(new { AlertId = alert.Id, order.Number }), ct);
        }
        return report;
    }, ct);
    public Task<RadiologyReport> Authorize(Scope scope, Guid id, VersionInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.RadiologyAuthorize); await CareGuard.Module(s, "radiology", ct);
        var order = await s.FindRadiologyOrderAsync(id, ct) ?? throw new DomainException("not_found", "Radiology order not found.", 404);
        CareGuard.Version(order.Version, input.Version);
        if (order.Status != "Reported") throw new DomainException("radiology.state", "Only reported studies can be authorized.", 409);
        var report = await s.FindRadiologyReportByOrderAsync(id, ct) ?? throw new DomainException("radiology.report.missing", "Record a report before authorization.", 409);
        if (report.ReportedBy == scope.Actor.Id) throw new DomainException("radiology.separation", "A second qualified clinician must authorize the report.", 403);
        var authorized = report with { Status = "Authorized", AuthorizedBy = scope.Actor.Id, AuthorizedAt = clock.UtcNow, Version = report.Version + 1 };
        authorized = authorized with { ContentHash = Phase3Rules.ReportHash(order, authorized) };
        await s.UpdateRadiologyReportAsync(authorized, ct);
        var updated = order with { Status = "Authorized", Version = order.Version + 1 };
        await s.UpdateRadiologyOrderAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "radiology.report.authorized", "RadiologyReport", report.Id, changes: new { ContentHash = authorized.ContentHash }), ct);
        return authorized;
    }, ct);
    public Task<RadiologyOrder> Cancel(Scope scope, Guid id, VersionInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.RadiologyOrder); await CareGuard.Module(s, "radiology", ct);
        var order = await s.FindRadiologyOrderAsync(id, ct) ?? throw new DomainException("not_found", "Radiology order not found.", 404);
        CareGuard.Version(order.Version, input.Version);
        if (order.Status is not ("Ordered" or "Scheduled")) throw new DomainException("radiology.state", "Performed studies cannot be cancelled.", 409);
        var reason = CareGuard.Text(input.Reason, 5, 500, "Cancellation reason");
        var updated = order with { Status = "Cancelled", Version = order.Version + 1 };
        await s.UpdateRadiologyOrderAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "radiology.order.cancelled", "RadiologyOrder", id, reason), ct);
        return updated;
    }, ct);
}

public sealed class EmergencyService(IUnitOfWork unitOfWork, IClock clock)
{
    public Task<EmergencyRegistration> Register(Scope scope, EmergencyRegistrationInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.EmergencyWrite); await CareGuard.Module(s, "emergency", ct);
        var name = CareGuard.Text(input.PatientName, 2, 160, "Patient name");
        if (!new[] { "Male", "Female", "Other", "Unknown" }.Contains(input.Sex)) throw new DomainException("emergency.fields", "Select a valid sex value.");
        if (!Phase3Rules.ArrivalModes.Contains(input.ArrivalMode)) throw new DomainException("emergency.fields", "Select a valid arrival mode.");
        if (input.DateOfBirth is DateOnly dob && (dob > scope.Today || dob < scope.Today.AddYears(-130))) throw new DomainException("patient.dob", "Review the date of birth.");
        if (input.PatientId is Guid patientId)
        {
            var patient = await s.FindPatientAsync(patientId, ct) ?? throw new DomainException("not_found", "Linked patient not found.", 404);
            if (!patient.FullName.Equals(name, StringComparison.OrdinalIgnoreCase)) throw new DomainException("emergency.identity", "The supplied name does not match the linked patient identity.", 409);
        }
        var number = await s.NextNumberAsync("ER", $"{scope.BranchId}:{clock.UtcNow.Year}", $"ER-{clock.UtcNow.Year}", ct);
        var registration = new EmergencyRegistration(Guid.NewGuid(), number, input.PatientId, name, input.Sex, input.DateOfBirth, input.ArrivalMode, "Open", clock.UtcNow, scope.Actor.Id)
        {
            ReferringFacility = input.ReferringFacility.Trim().Length <= 200 ? input.ReferringFacility.Trim() : "",
            BroughtBy = input.BroughtBy.Trim().Length <= 120 ? input.BroughtBy.Trim() : "",
            MedicoLegalCase = input.MedicoLegalCase
        };
        await s.InsertEmergencyRegistrationAsync(registration, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "emergency.registration.created", "EmergencyRegistration", registration.Id, changes: new { input.ArrivalMode, Linked = input.PatientId is not null }), ct);
        return registration;
    }, ct);
    public Task<TriageAssessment> Triage(Scope scope, Guid id, TriageInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.EmergencyTriage); await CareGuard.Module(s, "emergency", ct);
        var registration = await s.FindEmergencyRegistrationAsync(id, ct) ?? throw new DomainException("not_found", "Emergency registration not found.", 404);
        if (registration.Status != "Open") throw new DomainException("emergency.state", "Only open emergency episodes accept triage.", 409);
        if (!Phase3Rules.TriageCategories.Contains(input.Category)) throw new DomainException("emergency.triage", "Select a valid triage category.");
        var complaint = CareGuard.Text(input.Complaint, 3, 1000, "Presenting complaint");
        ClinicalRules.ValidateVitals(input.Vitals);
        var assessment = new TriageAssessment(Guid.NewGuid(), id, input.Category, complaint, input.Vitals, scope.Actor.Id, clock.UtcNow);
        await s.InsertTriageAssessmentAsync(assessment, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "emergency.triage.recorded", "TriageAssessment", assessment.Id, changes: new { input.Category }), ct);
        return assessment;
    }, ct);
    public Task<EmergencyObservation> Observe(Scope scope, Guid id, EmergencyObservationInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.EmergencyWrite); await CareGuard.Module(s, "emergency", ct);
        var registration = await s.FindEmergencyRegistrationAsync(id, ct) ?? throw new DomainException("not_found", "Emergency registration not found.", 404);
        if (registration.Status != "Open") throw new DomainException("emergency.state", "Only open emergency episodes accept observations.", 409);
        var note = CareGuard.Text(input.Note, 3, 2000, "Observation");
        ClinicalRules.ValidateVitals(input.Vitals);
        var observation = new EmergencyObservation(Guid.NewGuid(), id, note, input.Vitals, scope.Actor.Id, clock.UtcNow);
        await s.InsertEmergencyObservationAsync(observation, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "emergency.observation.recorded", "EmergencyObservation", observation.Id), ct);
        return observation;
    }, ct);
    public Task<EmergencyRegistration> Dispose(Scope scope, Guid id, DispositionInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.EmergencyDispose); await CareGuard.Module(s, "emergency", ct);
        var registration = await s.FindEmergencyRegistrationAsync(id, ct) ?? throw new DomainException("not_found", "Emergency registration not found.", 404);
        if (registration.Status != "Open") throw new DomainException("emergency.state", "This episode already has a disposition.", 409);
        if (!Phase3Rules.DispositionKinds.Contains(input.Kind)) throw new DomainException("emergency.disposition", "Select a valid disposition.");
        var summary = CareGuard.Text(input.Summary, 10, 5000, "Disposition summary");
        Guid? admissionId = null;
        if (input.Kind == "Admit")
        {
            if (input.BedId is not Guid bedId || input.DoctorId is not Guid doctorId) throw new DomainException("emergency.disposition", "Admission requires a bed and a responsible practitioner.");
            if (registration.PatientId is null) throw new DomainException("emergency.identity", "Link the episode to a registered patient before admission.", 409);
            if (!await s.HasActivePractitionerAsync(doctorId, ct)) throw new DomainException("admission.reference", "Select an active practitioner.");
            var bed = await s.FindBedAsync(bedId, ct) ?? throw new DomainException("not_found", "Bed not found.", 404);
            if (bed.Status != "Available" || await s.FindActiveAdmissionByBedAsync(bed.Id, ct) is not null) throw new DomainException("bed.occupied", "The selected bed is not available.", 409);
            if (await s.FindActiveAdmissionByPatientAsync(registration.PatientId.Value, ct) is not null) throw new DomainException("admission.active", "The patient already has an active admission.", 409);
            var number = await s.NextNumberAsync("ADMISSION", $"{scope.BranchId}:{clock.UtcNow.Year}", $"ADM-{clock.UtcNow.Year}", ct);
            var admission = new Admission(Guid.NewGuid(), number, registration.PatientId.Value, bed.Id, doctorId, "Active", summary, clock.UtcNow, null, "", "", "");
            await s.InsertAdmissionAsync(admission, ct);
            await s.UpdateBedAsync(bed with { Status = "Occupied", Version = bed.Version + 1 }, ct);
            await s.InsertBedEventAsync(new BedEvent(Guid.NewGuid(), admission.Id, bed.Id, "Admitted", "Available", "Occupied", scope.Actor.Id, clock.UtcNow, "Emergency admission"), ct);
            admissionId = admission.Id;
        }
        if (input.Kind == "Refer" && (input.ReceivingFacility is not string facility || facility.Trim().Length is < 3 or > 200))
            throw new DomainException("emergency.disposition", "Referral requires a receiving facility of 3–200 characters.");
        var disposition = new EmergencyDisposition(Guid.NewGuid(), id, input.Kind, summary, admissionId, input.Kind == "Refer" ? input.ReceivingFacility?.Trim() : null, scope.Actor.Id, clock.UtcNow);
        await s.InsertEmergencyDispositionAsync(disposition, ct);
        var updated = registration with { Status = "Dispositioned", Version = registration.Version + 1 };
        await s.UpdateEmergencyRegistrationAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "emergency.disposition.recorded", "EmergencyRegistration", id, summary, new { input.Kind, AdmissionId = admissionId }), ct);
        return updated;
    }, ct);
}

public sealed class OtService(IUnitOfWork unitOfWork, IClock clock)
{
    public Task<OtBooking> Request(Scope scope, OtBookingInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.OtRequest); await CareGuard.Module(s, "ot", ct);
        if (await s.FindPatientAsync(input.PatientId, ct) is not { Active: true }) throw new DomainException("ot.reference", "Select an active patient.");
        if (input.AdmissionId is Guid admissionId)
        {
            var linked = await s.FindAdmissionAsync(admissionId, ct) ?? throw new DomainException("ot.reference", "Linked admission must be active.");
            if (linked.Status != "Active") throw new DomainException("ot.reference", "Linked admission must be active.");
            if (linked.PatientId != input.PatientId) throw new DomainException("ot.reference", "The admission belongs to a different patient.");
        }
        if (!await s.HasActivePractitionerAsync(input.SurgeonId, ct)) throw new DomainException("ot.reference", "Select an active surgeon.");
        var procedure = CareGuard.Text(input.Procedure, 3, 200, "Procedure");
        Phase3Rules.ValidateLaterality(input.Site, input.Laterality);
        var site = CareGuard.Text(input.Site, 2, 120, "Surgical site");
        if (!Phase3Rules.Anaesthetics.Contains(input.Anaesthesia)) throw new DomainException("ot.fields", "Select a valid anaesthesia type.");
        if (!Phase3Rules.OtPriorities.Contains(input.Priority)) throw new DomainException("ot.fields", "Select a valid priority.");
        var number = await s.NextNumberAsync("OT", $"{scope.BranchId}:{clock.UtcNow.Year}", $"OT-{clock.UtcNow.Year}", ct);
        var booking = new OtBooking(Guid.NewGuid(), number, input.PatientId, input.AdmissionId, procedure, input.Laterality, site, input.SurgeonId, input.Anaesthesia, input.Priority,
            "Requested", "", null, null, clock.UtcNow, scope.Actor.Id, null, null, "");
        await s.InsertOtBookingAsync(booking, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "ot.booking.requested", "OtBooking", booking.Id, procedure, new { input.Anaesthesia, input.Priority }), ct);
        return booking;
    }, ct);
    public Task<OtBooking> Schedule(Scope scope, Guid id, OtScheduleInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.OtSchedule); await CareGuard.Module(s, "ot", ct);
        var booking = await s.FindOtBookingAsync(id, ct) ?? throw new DomainException("not_found", "Theatre booking not found.", 404);
        CareGuard.Version(booking.Version, input.Version);
        if (booking.Status != "Requested") throw new DomainException("ot.state", "Only requested bookings can be scheduled.", 409);
        var theatre = CareGuard.Text(input.Theatre, 2, 40, "Theatre");
        if (input.Start < clock.UtcNow.AddMinutes(-5) || input.Start > clock.UtcNow.AddDays(90)) throw new DomainException("ot.schedule", "Schedule surgery between now and the next 90 days.");
        if (input.DurationMinutes is < 15 or > 480) throw new DomainException("ot.schedule", "Surgery duration must be 15–480 minutes.");
        var end = input.Start.AddMinutes(input.DurationMinutes);
        if (await s.HasOtConflictAsync(theatre, input.Start, end, id, ct)) throw new DomainException("ot.conflict", "The theatre is already booked for this period.", 409);
        var updated = booking with { Theatre = theatre, ScheduledStart = input.Start, ScheduledEnd = end, Status = "Scheduled", Version = booking.Version + 1 };
        await s.UpdateOtBookingAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "ot.booking.scheduled", "OtBooking", id, reason: theatre, changes: new { input.Start, input.DurationMinutes }), ct);
        return updated;
    }, ct);
    public Task<OtChecklist> Checklist(Scope scope, Guid id, OtChecklistInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.OtRecord); await CareGuard.Module(s, "ot", ct);
        var booking = await s.FindOtBookingAsync(id, ct) ?? throw new DomainException("not_found", "Theatre booking not found.", 404);
        if (booking.Status != "Scheduled") throw new DomainException("ot.state", "Record the pre-op checklist after scheduling.", 409);
        if (await s.FindOtChecklistAsync(id, ct) is not null) throw new DomainException("ot.checklist.exists", "A pre-op checklist is already recorded.", 409);
        if (!input.Consent || !input.AnaesthesiaAssessment || !input.FastingConfirmed || !input.SiteMarked || !input.SafetyChecklist)
            throw new DomainException("ot.checklist", "All safety checklist items must be confirmed before surgery.", 409);
        var notes = input.Notes.Trim();
        if (notes.Length > 1000) throw new DomainException("care.fields", "Checklist notes must be at most 1000 characters.");
        var checklist = new OtChecklist(Guid.NewGuid(), id, true, true, true, true, true, notes, scope.Actor.Id, clock.UtcNow);
        await s.InsertOtChecklistAsync(checklist, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "ot.checklist.recorded", "OtChecklist", checklist.Id), ct);
        return checklist;
    }, ct);
    public Task<OtBooking> TimeIn(Scope scope, Guid id, VersionInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.OtRecord); await CareGuard.Module(s, "ot", ct);
        var booking = await s.FindOtBookingAsync(id, ct) ?? throw new DomainException("not_found", "Theatre booking not found.", 404);
        CareGuard.Version(booking.Version, input.Version);
        if (booking.Status != "Scheduled") throw new DomainException("ot.state", "Only scheduled surgeries can move into theatre.", 409);
        if (await s.FindOtChecklistAsync(id, ct) is null) throw new DomainException("ot.checklist", "Record the pre-op safety checklist before time-in.", 409);
        var updated = booking with { Status = "In Theatre", TimeIn = clock.UtcNow, Version = booking.Version + 1 };
        await s.UpdateOtBookingAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "ot.time.in", "OtBooking", id), ct);
        return updated;
    }, ct);
    public Task<OtBooking> TimeOut(Scope scope, Guid id, VersionInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.OtRecord); await CareGuard.Module(s, "ot", ct);
        var booking = await s.FindOtBookingAsync(id, ct) ?? throw new DomainException("not_found", "Theatre booking not found.", 404);
        CareGuard.Version(booking.Version, input.Version);
        if (booking.Status != "In Theatre") throw new DomainException("ot.state", "Only surgeries in theatre can record time-out.", 409);
        var updated = booking with { Status = "Recovery", TimeOut = clock.UtcNow, Version = booking.Version + 1 };
        await s.UpdateOtBookingAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "ot.time.out", "OtBooking", id), ct);
        return updated;
    }, ct);
    public Task<OtBooking> Complete(Scope scope, Guid id, OtCompleteInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.OtRecord); await CareGuard.Module(s, "ot", ct);
        var booking = await s.FindOtBookingAsync(id, ct) ?? throw new DomainException("not_found", "Theatre booking not found.", 404);
        CareGuard.Version(booking.Version, input.Version);
        if (booking.Status != "Recovery") throw new DomainException("ot.state", "Record time-out before completing the surgery.", 409);
        var note = CareGuard.Text(input.OperationNote, 20, 8000, "Operation note");
        var updated = booking with { Status = "Completed", OperationNote = note, Version = booking.Version + 1 };
        await s.UpdateOtBookingAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "ot.booking.completed", "OtBooking", id), ct);
        return updated;
    }, ct);
    public Task<OtBooking> Cancel(Scope scope, Guid id, VersionInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.OtSchedule); await CareGuard.Module(s, "ot", ct);
        var booking = await s.FindOtBookingAsync(id, ct) ?? throw new DomainException("not_found", "Theatre booking not found.", 404);
        CareGuard.Version(booking.Version, input.Version);
        if (booking.Status is not ("Requested" or "Scheduled")) throw new DomainException("ot.state", "Surgeries already in theatre or completed cannot be cancelled.", 409);
        var reason = CareGuard.Text(input.Reason, 5, 500, "Cancellation reason");
        var updated = booking with { Status = "Cancelled", Version = booking.Version + 1 };
        await s.UpdateOtBookingAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "ot.booking.cancelled", "OtBooking", id, reason), ct);
        return updated;
    }, ct);
}

public sealed class BloodBankService(IUnitOfWork unitOfWork, IClock clock)
{
    public Task<BloodUnit> ReceiveUnit(Scope scope, ReceiveUnitInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.BloodReceive); await CareGuard.Module(s, "blood", ct);
        var unitNumber = input.UnitNumber.Trim().ToUpperInvariant();
        if (unitNumber.Length is < 3 or > 40) throw new DomainException("blood.fields", "Unit number must be 3–40 characters.");
        if (!BloodRules.Groups.Contains(input.BloodGroup)) throw new DomainException("blood.group", "Select a valid blood group.");
        if (!BloodRules.Components.Contains(input.Component)) throw new DomainException("blood.fields", "Select a valid blood component.");
        if (await s.FindBloodUnitByNumberAsync(unitNumber, ct) is not null) throw new DomainException("blood.duplicate", "A unit with this number already exists.", 409);
        var maxDays = input.Component switch { "Platelets" => 7, "FFP" => 366, _ => 42 };
        if (input.ExpiryDate <= scope.Today || input.ExpiryDate > scope.Today.AddDays(maxDays))
            throw new DomainException("blood.expiry", $"Expiry must be within the storage window for {input.Component}.");
        var donor = CareGuard.Text(input.DonorName, 2, 160, "Donor name");
        var unit = new BloodUnit(Guid.NewGuid(), unitNumber, input.BloodGroup, input.Component, input.ExpiryDate, "Quarantined", donor, clock.UtcNow);
        await s.InsertBloodUnitAsync(unit, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "blood.unit.received", "BloodUnit", unit.Id, changes: new { unit.BloodGroup, unit.Component, unit.ExpiryDate }), ct);
        return unit;
    }, ct);
    public Task<BloodUnit> Screen(Scope scope, Guid id, ScreeningInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.BloodScreen); await CareGuard.Module(s, "blood", ct);
        var unit = await s.FindBloodUnitAsync(id, ct) ?? throw new DomainException("not_found", "Blood unit not found.", 404);
        if (unit.Status != "Quarantined") throw new DomainException("blood.state", "Only quarantined units can be screened.", 409);
        if (await s.FindScreeningAsync(id, ct) is not null) throw new DomainException("blood.screening.exists", "Screening results are already recorded for this unit.", 409);
        var notes = input.Notes.Trim();
        if ((input.Hbsag || input.Hiv || input.Hcv || input.Vdrl) && notes.Length < 5) throw new DomainException("blood.screening", "Reactive screening results require a note of at least 5 characters.");
        if (notes.Length > 500) throw new DomainException("care.fields", "Screening notes must be at most 500 characters.");
        await s.InsertScreeningAsync(new BloodScreening(Guid.NewGuid(), id, input.Hbsag, input.Hiv, input.Hcv, input.Vdrl, notes, scope.Actor.Id, clock.UtcNow), ct);
        var reactive = input.Hbsag || input.Hiv || input.Hcv || input.Vdrl;
        var updated = unit with { Status = reactive ? "Discarded" : "Available", Version = unit.Version + 1 };
        await s.UpdateBloodUnitAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, reactive ? "blood.unit.discarded" : "blood.unit.released", "BloodUnit", id, notes), ct);
        return updated;
    }, ct);
    public Task<Crossmatch> Crossmatch(Scope scope, CrossmatchInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.BloodCrossmatch); await CareGuard.Module(s, "blood", ct);
        var unit = await s.FindBloodUnitAsync(input.UnitId, ct) ?? throw new DomainException("not_found", "Blood unit not found.", 404);
        if (unit.Status != "Available") throw new DomainException("blood.state", "Only released units can be crossmatched.", 409);
        var patient = await s.FindPatientAsync(input.PatientId, ct) ?? throw new DomainException("not_found", "Patient not found.", 404);
        if (patient.BloodGroup == "Unknown" || !BloodRules.Groups.Contains(patient.BloodGroup)) throw new DomainException("blood.group", "The patient blood group is not established.", 409);
        if (!BloodRules.Compatible(unit.BloodGroup, patient.BloodGroup, unit.Component))
        {
            await s.AddAuditAsync(CareGuard.Event(scope, clock, "blood.crossmatch.rejected", "BloodUnit", unit.Id, $"Incompatible unit {unit.BloodGroup} for recipient {patient.BloodGroup}"), ct);
            throw new DomainException("blood.incompatible", "The unit is not compatible with the recipient blood group.", 409);
        }
        var number = await s.NextNumberAsync("CROSSMATCH", $"{scope.BranchId}:{clock.UtcNow.Year}", $"XM-{clock.UtcNow.Year}", ct);
        var crossmatch = new Crossmatch(Guid.NewGuid(), number, unit.Id, patient.Id, "Compatible", "Matched", scope.Actor.Id, clock.UtcNow);
        await s.InsertCrossmatchAsync(crossmatch, ct);
        var updated = unit with { Status = "Reserved", Version = unit.Version + 1 };
        await s.UpdateBloodUnitAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "blood.crossmatch.matched", "Crossmatch", crossmatch.Id, changes: new { unit.BloodGroup, Recipient = patient.BloodGroup }), ct);
        return crossmatch;
    }, ct);
    public Task<BloodIssue> Issue(Scope scope, BloodIssueInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.BloodIssue); await CareGuard.Module(s, "blood", ct);
        var unit = await s.FindBloodUnitAsync(input.UnitId, ct) ?? throw new DomainException("not_found", "Blood unit not found.", 404);
        if (unit.Status != "Reserved") throw new DomainException("blood.state", "Crossmatch a reserved unit before issue.", 409);
        if (await s.FindActiveCrossmatchAsync(unit.Id, input.PatientId, ct) is null) throw new DomainException("blood.crossmatch.missing", "No active crossmatch exists for this patient and unit.", 409);
        if (unit.ExpiryDate <= scope.Today) throw new DomainException("blood.expired", "The unit is expired and cannot be issued.", 409);
        if (input.AdmissionId is Guid admissionId && await s.FindAdmissionAsync(admissionId, ct) is not { Status: "Active" }) throw new DomainException("blood.reference", "Linked admission must be active.");
        var notes = input.Notes.Trim();
        if (notes.Length > 500) throw new DomainException("care.fields", "Issue notes must be at most 500 characters.");
        var number = await s.NextNumberAsync("BLOOD", $"{scope.BranchId}:{clock.UtcNow.Year}", $"BLD-{clock.UtcNow.Year}", ct);
        var issue = new BloodIssue(Guid.NewGuid(), number, unit.Id, input.PatientId, input.AdmissionId, "Issued", "", "", scope.Actor.Id, clock.UtcNow, null, notes);
        await s.InsertBloodIssueAsync(issue, ct);
        var updated = unit with { Status = "Issued", Version = unit.Version + 1 };
        await s.UpdateBloodUnitAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "blood.unit.issued", "BloodIssue", issue.Id, changes: new { unit.UnitNumber, input.PatientId }), ct);
        return issue;
    }, ct);
    public Task<BloodIssue> Transfuse(Scope scope, Guid id, TransfuseInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.BloodTransfuse); await CareGuard.Module(s, "blood", ct);
        var issue = await s.FindBloodIssueAsync(id, ct) ?? throw new DomainException("not_found", "Blood issue not found.", 404);
        CareGuard.Version(issue.Version, input.Version);
        if (issue.Status != "Issued") throw new DomainException("blood.state", "Only issued units can be transfused.", 409);
        var reaction = input.Reaction.Trim();
        if (reaction.Length > 1000) throw new DomainException("care.fields", "Reaction record must be at most 1000 characters.");
        var notes = input.Notes.Trim();
        if (notes.Length > 500) throw new DomainException("care.fields", "Transfusion notes must be at most 500 characters.");
        var updated = issue with { Status = "Transfused", Reaction = reaction, TransfusedAt = clock.UtcNow, Notes = notes, Version = issue.Version + 1 };
        await s.UpdateBloodIssueAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "blood.transfusion.recorded", "BloodIssue", id, changes: new { Reaction = reaction }), ct);
        if (reaction.Length >= 3 && reaction is not ("Nil" or "None" or "No reaction"))
        {
            var alert = new CriticalAlert(Guid.NewGuid(), "Blood", id, issue.PatientId, $"Transfusion reaction recorded: {reaction}", "Critical", "Open", clock.UtcNow, null, null, "");
            await s.InsertCriticalAlertAsync(alert, ct);
            await s.AddOutboxAsync("blood.reaction", id, JsonSerializer.Serialize(new { AlertId = alert.Id, issue.Number }), ct);
        }
        return updated;
    }, ct);
    public Task<BloodIssue> Return(Scope scope, Guid id, ReturnInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.BloodIssue); await CareGuard.Module(s, "blood", ct);
        var issue = await s.FindBloodIssueAsync(id, ct) ?? throw new DomainException("not_found", "Blood issue not found.", 404);
        CareGuard.Version(issue.Version, input.Version);
        if (issue.Status != "Issued") throw new DomainException("blood.state", "Only issued, not yet transfused units can be returned.", 409);
        var notes = CareGuard.Text(input.Notes, 5, 500, "Return assessment");
        var unit = await s.FindBloodUnitAsync(issue.UnitId, ct) ?? throw new DomainException("not_found", "Blood unit not found.", 404);
        var updated = issue with { Status = "Returned", ReturnAssessment = input.AssessedFit ? "Fit for restock" : "Unfit — discarded", Notes = notes, Version = issue.Version + 1 };
        await s.UpdateBloodIssueAsync(updated, ct);
        var restocked = unit with { Status = input.AssessedFit ? "Available" : "Discarded", Version = unit.Version + 1 };
        await s.UpdateBloodUnitAsync(restocked, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "blood.unit.returned", "BloodIssue", id, notes, new { Fit = input.AssessedFit }), ct);
        return updated;
    }, ct);
    public Task<BloodUnit> Dispose(Scope scope, Guid id, VersionInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.BloodScreen); await CareGuard.Module(s, "blood", ct);
        var unit = await s.FindBloodUnitAsync(id, ct) ?? throw new DomainException("not_found", "Blood unit not found.", 404);
        CareGuard.Version(unit.Version, input.Version);
        if (unit.Status is not ("Quarantined" or "Available" or "Reserved")) throw new DomainException("blood.state", "Issued or transfused units cannot be disposed directly.", 409);
        var reason = CareGuard.Text(input.Reason, 5, 500, "Disposal reason");
        var updated = unit with { Status = "Discarded", Version = unit.Version + 1 };
        await s.UpdateBloodUnitAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "blood.unit.disposed", "BloodUnit", id, reason), ct);
        return updated;
    }, ct);
}
