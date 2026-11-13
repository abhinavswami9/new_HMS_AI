using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Careflow.Domain;
namespace Careflow.Application;

internal static class CareGuard
{
    public static async Task Module(IHospitalStore s, string key, CancellationToken ct)
    {
        if (await s.FindModuleAsync(key, ct) is not { Enabled: true }) throw new DomainException("module.disabled", $"The {key} module is disabled for this branch.");
    }
    public static void Version(long actual, long expected)
    {
        if (actual != expected) throw new DomainException("concurrency", "This record changed in another session. Refresh and try again.", 409);
    }
    public static AuditEvent Event(Scope scope, IClock clock, string action, string entity, Guid id, string reason = "", object? changes = null) =>
        new(Guid.NewGuid(), scope.Actor.Id, scope.Actor.Name, action, entity, id, reason, clock.UtcNow, changes is null ? null : JsonSerializer.Serialize(changes));
    public static string Text(string value, int min, int max, string field)
    {
        var trimmed = value.Trim();
        if (trimmed.Length < min || trimmed.Length > max) throw new DomainException("care.fields", $"{field} must be {min}–{max} characters.");
        return trimmed;
    }
}

public sealed class InpatientService(IUnitOfWork unitOfWork, IClock clock)
{
    public Task<Admission> Admit(Scope scope, AdmissionInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.IpdWrite); await CareGuard.Module(s, "ipd", ct);
        if (await s.FindPatientAsync(input.PatientId, ct) is not { Active: true } || !await s.HasActivePractitionerAsync(input.DoctorId, ct))
            throw new DomainException("admission.reference", "Select an active patient and practitioner.");
        var bed = await s.FindBedAsync(input.BedId, ct) ?? throw new DomainException("not_found", "Bed not found.", 404);
        if (bed.Status != "Available" || await s.FindActiveAdmissionByBedAsync(bed.Id, ct) is not null) throw new DomainException("bed.occupied", "The selected bed is not available.", 409);
        if (await s.FindActiveAdmissionByPatientAsync(input.PatientId, ct) is not null) throw new DomainException("admission.active", "The patient already has an active admission.", 409);
        var reason = CareGuard.Text(input.Reason, 5, 1000, "Admission reason");
        var number = await s.NextNumberAsync("ADMISSION", $"{scope.BranchId}:{clock.UtcNow.Year}", $"ADM-{clock.UtcNow.Year}", ct);
        var admission = new Admission(Guid.NewGuid(), number, input.PatientId, bed.Id, input.DoctorId, "Active", reason, clock.UtcNow, null, "", "", "");
        await s.InsertAdmissionAsync(admission, ct);
        await s.UpdateBedAsync(bed with { Status = "Occupied", Version = bed.Version + 1 }, ct);
        await s.InsertBedEventAsync(new BedEvent(Guid.NewGuid(), admission.Id, bed.Id, "Admitted", "Available", "Occupied", scope.Actor.Id, clock.UtcNow, reason), ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "admission.created", "Admission", admission.Id, reason), ct);
        await s.AddOutboxAsync("admission.created", admission.Id, JsonSerializer.Serialize(new { admission.Id, admission.PatientId, admission.BedId }), ct);
        return admission;
    }, ct);
    public Task<Admission> Transfer(Scope scope, Guid id, TransferInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.IpdWrite); await CareGuard.Module(s, "ipd", ct);
        var admission = await s.FindAdmissionAsync(id, ct) ?? throw new DomainException("not_found", "Admission not found.", 404);
        CareGuard.Version(admission.Version, input.Version);
        if (admission.Status != "Active") throw new DomainException("admission.state", "Only active admissions can be transferred.", 409);
        var target = await s.FindBedAsync(input.BedId, ct) ?? throw new DomainException("not_found", "Target bed not found.", 404);
        if (target.Id == admission.BedId || target.Status != "Available" || await s.FindActiveAdmissionByBedAsync(target.Id, ct) is not null) throw new DomainException("bed.occupied", "Select a different available bed.", 409);
        var reason = CareGuard.Text(input.Reason, 5, 1000, "Transfer reason");
        var old = await s.FindBedAsync(admission.BedId, ct) ?? throw new DomainException("not_found", "Current bed not found.", 404);
        await s.UpdateBedAsync(old with { Status = "Cleaning", Version = old.Version + 1 }, ct);
        await s.UpdateBedAsync(target with { Status = "Occupied", Version = target.Version + 1 }, ct);
        var updated = admission with { BedId = target.Id, Version = admission.Version + 1 };
        await s.UpdateAdmissionAsync(updated, ct);
        await s.InsertBedEventAsync(new BedEvent(Guid.NewGuid(), id, old.Id, "Transfer out", "Occupied", "Cleaning", scope.Actor.Id, clock.UtcNow, reason), ct);
        await s.InsertBedEventAsync(new BedEvent(Guid.NewGuid(), id, target.Id, "Transfer in", "Available", "Occupied", scope.Actor.Id, clock.UtcNow, reason), ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "admission.transferred", "Admission", id, reason, new { From = old.Id, To = target.Id }), ct);
        return updated;
    }, ct);
    public Task<Admission> Discharge(Scope scope, Guid id, DischargeInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.IpdDischarge); await CareGuard.Module(s, "ipd", ct);
        var admission = await s.FindAdmissionAsync(id, ct) ?? throw new DomainException("not_found", "Admission not found.", 404);
        CareGuard.Version(admission.Version, input.Version);
        if (admission.Status != "Active") throw new DomainException("admission.state", "Only active admissions can be discharged.", 409);
        var summary = CareGuard.Text(input.Summary, 20, 10000, "Discharge summary");
        var diagnosis = CareGuard.Text(input.Diagnosis, 3, 1000, "Discharge diagnosis");
        var instructions = CareGuard.Text(input.Instructions, 10, 5000, "Discharge instructions");
        var bed = await s.FindBedAsync(admission.BedId, ct) ?? throw new DomainException("not_found", "Bed not found.", 404);
        var updated = admission with { Status = "Discharged", DischargedAt = clock.UtcNow, DischargeSummary = summary, DischargeDiagnosis = diagnosis, DischargeInstructions = instructions, Version = admission.Version + 1 };
        await s.UpdateAdmissionAsync(updated, ct);
        await s.UpdateBedAsync(bed with { Status = "Cleaning", Version = bed.Version + 1 }, ct);
        await s.InsertBedEventAsync(new BedEvent(Guid.NewGuid(), id, bed.Id, "Discharged", "Occupied", "Cleaning", scope.Actor.Id, clock.UtcNow, "Clinical discharge completed"), ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "admission.discharged", "Admission", id, "Signed discharge summary recorded"), ct);
        await s.AddOutboxAsync("admission.discharged", id, JsonSerializer.Serialize(new { AdmissionId = id, summary }), ct);
        return updated;
    }, ct);
    public Task<Bed> SetBedStatus(Scope scope, Guid id, BedStatusInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.IpdBed); await CareGuard.Module(s, "ipd", ct);
        var bed = await s.FindBedAsync(id, ct) ?? throw new DomainException("not_found", "Bed not found.", 404);
        CareGuard.Version(bed.Version, input.Version);
        var allowed = (bed.Status, input.Status) is ("Cleaning", "Available") or ("Maintenance", "Available") or ("Available", "Maintenance");
        if (!allowed) throw new DomainException("bed.transition", "This bed status transition is not allowed. Occupied beds change through admission workflows.", 409);
        var reason = CareGuard.Text(input.Reason, 5, 500, "Bed status reason");
        var updated = bed with { Status = input.Status, Version = bed.Version + 1 };
        await s.UpdateBedAsync(updated, ct);
        await s.InsertBedEventAsync(new BedEvent(Guid.NewGuid(), null, id, "Status changed", bed.Status, input.Status, scope.Actor.Id, clock.UtcNow, reason), ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "bed.status.changed", "Bed", id, reason), ct);
        return updated;
    }, ct);
    public Task<NursingNote> AddNursingNote(Scope scope, NursingNoteInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.NursingWrite); await CareGuard.Module(s, "ipd", ct); await CareGuard.Module(s, "nursing", ct);
        var admission = await s.FindAdmissionAsync(input.AdmissionId, ct) ?? throw new DomainException("not_found", "Admission not found.", 404);
        if (admission.Status != "Active") throw new DomainException("admission.state", "Nursing records require an active admission.", 409);
        if (!new[] { "Assessment", "Progress", "IntakeOutput" }.Contains(input.Kind)) throw new DomainException("nursing.kind", "Unsupported nursing note type.");
        var text = CareGuard.Text(input.Text, 5, 5000, "Nursing note");
        ClinicalRules.ValidateVitals(input.Vitals);
        if (input.IntakeMl is < 0 or > 20000 || input.OutputMl is < 0 or > 20000 || input.PainScore is < 0 or > 10 || !new[] { "Low", "Moderate", "High" }.Contains(input.FallRisk))
            throw new DomainException("nursing.range", "Review intake/output, pain score and fall risk.");
        var note = new NursingNote(Guid.NewGuid(), admission.Id, admission.PatientId, input.Kind, text, input.Vitals, input.IntakeMl, input.OutputMl, input.PainScore, input.FallRisk, scope.Actor.Id, clock.UtcNow);
        await s.InsertNursingNoteAsync(note, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "nursing.note.recorded", "NursingNote", note.Id), ct);
        var policyJson = await s.FindPolicyAsync("nursing.escalation", ct) ?? throw new DomainException("policy.missing", "An approved nursing escalation policy must be configured before recording observations.", 409);
        var policy = JsonSerializer.Deserialize<NursingEscalationPolicy>(policyJson, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? throw new DomainException("policy.invalid", "The nursing escalation policy is invalid.", 409);
        if (policy.Spo2Below is < 1 or > 100 || policy.SystolicBelow is < 1 or > 350 || policy.SystolicAbove is < 1 or > 350 || policy.SystolicBelow >= policy.SystolicAbove || policy.PulseBelow is < 1 or > 300 || policy.PulseAbove is < 1 or > 300 || policy.PulseBelow >= policy.PulseAbove || policy.TemperatureAtOrAbove is < 25 or > 45 || policy.PainAtOrAbove is < 0 or > 10)
            throw new DomainException("policy.invalid", "The nursing escalation policy contains invalid thresholds.", 409);
        var alerts = new List<string>();
        if (input.Vitals.Spo2 < policy.Spo2Below) alerts.Add($"Oxygen saturation below {policy.Spo2Below}%");
        if (input.Vitals.Systolic < policy.SystolicBelow || input.Vitals.Systolic > policy.SystolicAbove) alerts.Add("Systolic blood pressure outside the configured escalation range");
        if (input.Vitals.Pulse < policy.PulseBelow || input.Vitals.Pulse > policy.PulseAbove) alerts.Add("Pulse outside the configured escalation range");
        if (input.Vitals.TemperatureC >= policy.TemperatureAtOrAbove) alerts.Add("High fever recorded");
        if (input.PainScore >= policy.PainAtOrAbove) alerts.Add("Severe pain score recorded");
        if (input.FallRisk == "High") alerts.Add("High fall risk recorded");
        foreach (var message in alerts)
        {
            var alert = new CriticalAlert(Guid.NewGuid(), "Nursing", note.Id, admission.PatientId, message, "High", "Open", clock.UtcNow, null, null, "");
            await s.InsertCriticalAlertAsync(alert, ct);
            await s.AddOutboxAsync("nursing.alert", alert.Id, JsonSerializer.Serialize(new { alert.Id, alert.PatientId, message }), ct);
        }
        return note;
    }, ct);
    public Task<MedicationOrder> OrderMedication(Scope scope, MedicationOrderInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.NursingOrder); await CareGuard.Module(s, "ipd", ct); await CareGuard.Module(s, "nursing", ct); await CareGuard.Module(s, "pharmacy", ct);
        var admission = await s.FindAdmissionAsync(input.AdmissionId, ct) ?? throw new DomainException("not_found", "Admission not found.", 404);
        if (admission.Status != "Active") throw new DomainException("admission.state", "Medication orders require an active admission.", 409);
        var medicine = (await s.LoadMedicinesAsync(ct)).FirstOrDefault(m => m.Id == input.MedicineId) ?? throw new DomainException("medicine.unknown", "Select an approved formulary medicine.");
        var dose = CareGuard.Text(input.Dose, 1, 80, "Dose"); var route = CareGuard.Text(input.Route, 1, 40, "Route"); var frequency = CareGuard.Text(input.Frequency, 1, 80, "Frequency");
        if (input.StartsAt < clock.UtcNow.AddMinutes(-5) || input.EndsAt.HasValue && input.EndsAt <= input.StartsAt) throw new DomainException("medication.window", "Review the medication start and stop time.");
        var chart = await s.LoadChartAsync(admission.PatientId, 1, ct);
        var allergies = chart?.Encounters.FirstOrDefault()?.Allergies ?? [];
        ClinicalRules.ValidatePrescription([new(medicine.Id, $"{medicine.GenericName} {medicine.Strength}", dose, frequency, 1, "")], allergies, new HashSet<Guid> { medicine.Id });
        var order = new MedicationOrder(Guid.NewGuid(), admission.Id, admission.PatientId, medicine.Id, $"{medicine.GenericName} {medicine.Strength}", dose, route, frequency, input.StartsAt, input.EndsAt, "Active", scope.Actor.Id, clock.UtcNow);
        await s.InsertMedicationOrderAsync(order, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "medication.ordered", "MedicationOrder", order.Id), ct);
        return order;
    }, ct);
    public Task<MedicationOrder> StopMedication(Scope scope, Guid id, VersionInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.NursingOrder); await CareGuard.Module(s, "nursing", ct);
        var order = await s.FindMedicationOrderAsync(id, ct) ?? throw new DomainException("not_found", "Medication order not found.", 404);
        CareGuard.Version(order.Version, input.Version);
        if (order.Status != "Active") throw new DomainException("medication.state", "Only active medication orders can be stopped.", 409);
        var updated = order with { Status = "Stopped", EndsAt = clock.UtcNow, Version = order.Version + 1 };
        await s.UpdateMedicationOrderAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "medication.stopped", "MedicationOrder", id, input.Reason), ct);
        return updated;
    }, ct);
    public Task<MedicationAdministration> Administer(Scope scope, AdministerInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.NursingAdminister); await CareGuard.Module(s, "ipd", ct); await CareGuard.Module(s, "nursing", ct); await CareGuard.Module(s, "pharmacy", ct);
        var order = await s.FindMedicationOrderAsync(input.OrderId, ct) ?? throw new DomainException("not_found", "Medication order not found.", 404);
        if (order.Status != "Active" || input.GivenAt < order.StartsAt || order.EndsAt.HasValue && input.GivenAt > order.EndsAt) throw new DomainException("medication.window", "The order is not active at the administration time.", 409);
        if (input.GivenAt > clock.UtcNow.AddMinutes(5)) throw new DomainException("medication.future", "Administration time cannot be in the future.");
        if (await s.HasAdministrationAtAsync(order.Id, input.GivenAt, ct)) throw new DomainException("medication.duplicate", "An administration is already recorded at this exact time.", 409);
        var admission = await s.FindAdmissionAsync(order.AdmissionId, ct) ?? throw new DomainException("not_found", "Admission not found.", 404);
        if (admission.Status != "Active") throw new DomainException("admission.state", "Administration requires an active admission.", 409);
        var batch = await s.FindStockBatchAsync(input.BatchId, ct) ?? throw new DomainException("not_found", "Stock batch not found.", 404);
        if (batch.MedicineId != order.MedicineId || batch.Status != "Active" || batch.ExpiryDate <= DateOnly.FromDateTime(clock.UtcNow.UtcDateTime) || batch.Quantity < input.Quantity || input.Quantity <= 0 || decimal.Round(input.Quantity, 3) != input.Quantity)
            throw new DomainException("stock.unavailable", "Select an unexpired in-stock batch for the ordered medicine with a positive quantity (max 3 decimals).", 409);
        var administration = new MedicationAdministration(Guid.NewGuid(), order.Id, order.AdmissionId, batch.Id, input.Quantity, input.GivenAt, scope.Actor.Id, CareGuard.Text(input.Witness, 0, 160, "Witness"), CareGuard.Text(input.Notes, 0, 1000, "Administration notes"));
        await s.InsertAdministrationAsync(administration, ct);
        await s.UpdateStockBatchAsync(batch with { Quantity = batch.Quantity - input.Quantity, Status = batch.Quantity - input.Quantity == 0 ? "Depleted" : "Active", Version = batch.Version + 1 }, batch.Quantity, ct);
        await s.InsertStockMovementAsync(new StockMovement(Guid.NewGuid(), batch.Id, "Administration", -input.Quantity, administration.Id.ToString(), scope.Actor.Id, clock.UtcNow), ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "medication.administered", "MedicationAdministration", administration.Id, changes: new { OrderId = order.Id, BatchId = batch.Id, input.Quantity }), ct);
        return administration;
    }, ct);
    public Task<ShiftHandover> CreateHandover(Scope scope, HandoverInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.NursingHandover); await CareGuard.Module(s, "nursing", ct);
        var summary = CareGuard.Text(input.Summary, 20, 5000, "Handover summary");
        var incoming = await s.FindUserAsync(input.ToNurseId, scope.HospitalId, scope.BranchId, ct);
        if (incoming is null || !incoming.Enabled || incoming.Id == scope.Actor.Id || !(incoming.Grants ?? Permissions.For(incoming.Role)).Contains(Permissions.NursingHandover))
            throw new DomainException("handover.target", "Select a different active nurse with handover permission in this branch.");
        if (input.PatientIds.Count is 0 or > 100) throw new DomainException("handover.patients", "Include 1–100 admitted patients.");
        foreach (var patientId in input.PatientIds.Distinct()) if (await s.FindActiveAdmissionByPatientAsync(patientId, ct) is null) throw new DomainException("handover.patient", "Every handover patient must have an active admission.");
        var handover = new ShiftHandover(Guid.NewGuid(), scope.Actor.Id, incoming.DisplayName, summary, input.PatientIds.Distinct().ToList(), "Pending", clock.UtcNow, null, null) { ToNurseId = incoming.Id };
        await s.InsertHandoverAsync(handover, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "nursing.handover.created", "ShiftHandover", handover.Id), ct);
        return handover;
    }, ct);
    public Task<ShiftHandover> AcknowledgeHandover(Scope scope, Guid id, VersionInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.NursingHandover); await CareGuard.Module(s, "nursing", ct);
        var handover = await s.FindHandoverAsync(id, ct) ?? throw new DomainException("not_found", "Handover not found.", 404);
        CareGuard.Version(handover.Version, input.Version);
        if (handover.Status != "Pending" || handover.ToNurseId != scope.Actor.Id) throw new DomainException("handover.state", "Only the named incoming nurse can acknowledge this handover.", 409);
        var updated = handover with { Status = "Acknowledged", AcknowledgedAt = clock.UtcNow, AcknowledgedBy = scope.Actor.Id, Version = handover.Version + 1 };
        await s.UpdateHandoverAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "nursing.handover.acknowledged", "ShiftHandover", id, input.Reason), ct);
        return updated;
    }, ct);
}

public sealed class PharmacyService(IUnitOfWork unitOfWork, IClock clock)
{
    public Task<StockBatch> ReceiveStock(Scope scope, ReceiveStockInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.PharmacyWrite); await CareGuard.Module(s, "pharmacy", ct);
        if (!(await s.LoadMedicinesAsync(ct)).Any(m => m.Id == input.MedicineId)) throw new DomainException("medicine.unknown", "Select an approved formulary medicine.");
        if (input.ExpiryDate <= DateOnly.FromDateTime(clock.UtcNow.UtcDateTime) || input.Quantity is <= 0 or > 100000 || input.UnitCost is < 0 or > 1000000 || decimal.Round(input.Quantity, 3) != input.Quantity || decimal.Round(input.UnitCost, 4) != input.UnitCost)
            throw new DomainException("stock.fields", "Review expiry, quantity (max 3 decimals) and unit cost (max 4 decimals).");
        var batchNumber = CareGuard.Text(input.BatchNumber, 2, 80, "Batch number"); var supplier = CareGuard.Text(input.Supplier, 2, 160, "Supplier"); var reference = CareGuard.Text(input.Reference, 3, 100, "GRN/reference");
        var batch = new StockBatch(Guid.NewGuid(), input.MedicineId, batchNumber, input.ExpiryDate, input.Quantity, input.UnitCost, supplier, reference, clock.UtcNow, scope.Actor.Id);
        await s.InsertStockBatchAsync(batch, ct);
        await s.InsertStockMovementAsync(new StockMovement(Guid.NewGuid(), batch.Id, "Receipt", batch.Quantity, reference, scope.Actor.Id, clock.UtcNow), ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "stock.received", "StockBatch", batch.Id, changes: new { batch.BatchNumber, batch.Quantity, batch.ExpiryDate }), ct);
        return batch;
    }, ct);
    public Task<Dispense> Dispense(Scope scope, DispenseInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.PharmacyDispense); await CareGuard.Module(s, "pharmacy", ct);
        var key = CareGuard.Text(input.IdempotencyKey, 8, 100, "Idempotency key");
        if (!new[] { "OPD", "IPD" }.Contains(input.Kind) || input.Lines.Count is 0 or > 50) throw new DomainException("dispense.fields", "Select OPD/IPD and 1–50 medicine lines.");
        var requested = new Dictionary<Guid, decimal>();
        foreach (var line in input.Lines)
        {
            if (line.Quantity is <= 0 or > 1000 || decimal.Round(line.Quantity, 3) != line.Quantity) throw new DomainException("dispense.quantity", "Quantity must be positive with at most three decimal places.");
            requested[line.MedicineId] = requested.GetValueOrDefault(line.MedicineId) + line.Quantity;
            if (requested[line.MedicineId] > 1000) throw new DomainException("dispense.quantity", "Total quantity per medicine cannot exceed 1000.");
        }
        var normalizedLines = requested.OrderBy(x => x.Key).Select(x => new { MedicineId = x.Key, Quantity = x.Value }).ToList();
        var requestHash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { SchemaVersion = 1, input.Kind, input.PatientId, EncounterId = input.Kind == "OPD" ? input.EncounterId : null, AdmissionId = input.Kind == "IPD" ? input.AdmissionId : null, Lines = normalizedLines })))).ToLowerInvariant();
        if (await s.FindDispenseByKeyAsync(key, ct) is { } existing)
        {
            if (existing.RequestHash != requestHash) throw new DomainException("dispense.idempotency", "This dispensing key was already used for a different request.", 409);
            return existing;
        }
        if (await s.FindPatientAsync(input.PatientId, ct) is not { Active: true }) throw new DomainException("patient.inactive", "Select an active patient.");
        Encounter? encounter = null; Admission? admission = null;
        if (input.Kind == "OPD")
        {
            encounter = input.EncounterId.HasValue ? await s.FindEncounterAsync(input.EncounterId.Value, ct) : null;
            if (encounter is null || encounter.Status != "Signed" || encounter.PatientId != input.PatientId) throw new DomainException("dispense.prescription", "OPD dispensing requires a signed prescription for this patient.", 409);
        }
        else
        {
            admission = input.AdmissionId.HasValue ? await s.FindAdmissionAsync(input.AdmissionId.Value, ct) : null;
            if (admission is null || admission.Status != "Active" || admission.PatientId != input.PatientId) throw new DomainException("dispense.admission", "IPD dispensing requires an active admission for this patient.", 409);
        }
        var medicines = await s.LoadMedicinesAsync(ct); var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var activeOrders = admission is null ? [] : await s.LoadActiveMedicationOrdersAsync(admission.Id, ct);
        var lines = new List<DispenseLine>(); var changed = new List<StockBatch>(); var movements = new List<StockMovement>(); var originalQuantities = new Dictionary<Guid, decimal>();
        foreach (var request in normalizedLines)
        {
            var medicine = medicines.FirstOrDefault(m => m.Id == request.MedicineId) ?? throw new DomainException("medicine.unknown", "Unknown medicine.");
            if (encounter is not null && !encounter.Prescription.Any(p => p.MedicineId == medicine.Id)) throw new DomainException("dispense.prescription", "The medicine is not on the signed prescription.");
            if (admission is not null && !activeOrders.Any(o => o.MedicineId == medicine.Id)) throw new DomainException("dispense.order", "IPD dispensing requires an active inpatient medication order for each medicine.", 409);
            var remaining = request.Quantity; var allocations = new List<BatchAllocation>();
            foreach (var batch in await s.LoadAvailableBatchesAsync(medicine.Id, today, ct))
            {
                if (remaining == 0) break;
                var take = Math.Min(remaining, batch.Quantity); remaining -= take;
                originalQuantities[batch.Id] = batch.Quantity;
                allocations.Add(new BatchAllocation(batch.Id, batch.BatchNumber, take));
                changed.Add(batch with { Quantity = batch.Quantity - take, Status = batch.Quantity - take == 0 ? "Depleted" : "Active", Version = batch.Version + 1 });
            }
            if (remaining > 0) throw new DomainException("stock.insufficient", $"Insufficient unexpired stock for {medicine.GenericName}.", 409);
            lines.Add(new DispenseLine(medicine.Id, $"{medicine.GenericName} {medicine.Strength}", request.Quantity, allocations));
        }
        var number = await s.NextNumberAsync("DISPENSE", $"{scope.BranchId}:{clock.UtcNow.Year}", $"DSP-{clock.UtcNow.Year}", ct);
        var dispense = new Dispense(Guid.NewGuid(), number, input.Kind, input.PatientId, input.Kind == "OPD" ? input.EncounterId : null, input.Kind == "IPD" ? input.AdmissionId : null, lines, key, clock.UtcNow, scope.Actor.Id) { RequestHash = requestHash };
        foreach (var batch in changed) await s.UpdateStockBatchAsync(batch, originalQuantities[batch.Id], ct);
        foreach (var line in lines) foreach (var allocation in line.Batches) movements.Add(new StockMovement(Guid.NewGuid(), allocation.BatchId, "Dispense", -allocation.Quantity, dispense.Id.ToString(), scope.Actor.Id, clock.UtcNow));
        foreach (var movement in movements) await s.InsertStockMovementAsync(movement, ct);
        await s.InsertDispenseAsync(dispense, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "pharmacy.dispensed", "Dispense", dispense.Id, changes: new { dispense.Number, dispense.Kind, Lines = lines }), ct);
        await s.AddOutboxAsync("pharmacy.dispensed", dispense.Id, JsonSerializer.Serialize(new { dispense.Id, dispense.PatientId }), ct);
        return dispense;
    }, ct);
}

public sealed class LaboratoryService(IUnitOfWork unitOfWork, IClock clock)
{
    public Task<LabOrder> Order(Scope scope, LabOrderInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.LabOrder); await CareGuard.Module(s, "laboratory", ct);
        if (await s.FindPatientAsync(input.PatientId, ct) is not { Active: true }) throw new DomainException("patient.inactive", "Select an active patient.");
        var test = await s.FindLabTestAsync(input.TestId, ct) ?? throw new DomainException("lab.test", "Select an active laboratory test.");
        if (!test.Active) throw new DomainException("lab.test", "The selected test is inactive.");
        if (!new[] { "Routine", "Urgent", "STAT" }.Contains(input.Priority)) throw new DomainException("lab.priority", "Unsupported priority.");
        if (input.EncounterId.HasValue) { var e = await s.FindEncounterAsync(input.EncounterId.Value, ct); if (e is null || e.PatientId != input.PatientId) throw new DomainException("lab.reference", "Encounter does not belong to this patient."); }
        if (input.AdmissionId.HasValue) { var a = await s.FindAdmissionAsync(input.AdmissionId.Value, ct); if (a is null || a.PatientId != input.PatientId) throw new DomainException("lab.reference", "Admission does not belong to this patient."); }
        var number = await s.NextNumberAsync("LAB", $"{scope.BranchId}:{clock.UtcNow.Year}", $"LAB-{clock.UtcNow.Year}", ct);
        var order = new LabOrder(Guid.NewGuid(), number, input.PatientId, input.EncounterId, input.AdmissionId, test.Id, input.Priority, "Ordered", null, null, scope.Actor.Id, clock.UtcNow, null, null, null, null, null, "") { ClinicalNote = CareGuard.Text(input.ClinicalNote, 0, 1000, "Clinical note") };
        await s.InsertLabOrderAsync(order, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "lab.ordered", "LabOrder", order.Id), ct);
        return order;
    }, ct);
    public Task<LabOrder> Collect(Scope scope, Guid id, CollectSpecimenInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.LabCollect); await CareGuard.Module(s, "laboratory", ct);
        var order = await s.FindLabOrderAsync(id, ct) ?? throw new DomainException("not_found", "Lab order not found.", 404);
        CareGuard.Version(order.Version, input.Version);
        if (order.Status != "Ordered") throw new DomainException("lab.state", "Only ordered tests can be collected.", 409);
        var test = await s.FindLabTestAsync(order.TestId, ct) ?? throw new DomainException("lab.test", "Test not found.");
        if (!string.Equals(input.SpecimenType.Trim(), test.SpecimenType, StringComparison.OrdinalIgnoreCase) || input.VolumeMl is <= 0 or > 1000 || decimal.Round(input.VolumeMl, 3) != input.VolumeMl) throw new DomainException("lab.specimen", "Specimen type or volume is invalid for this test.");
        var barcode = await s.NextNumberAsync("LAB_BARCODE", scope.BranchId.ToString(), $"BC-{clock.UtcNow.Year}", ct);
        var accession = await s.NextNumberAsync("LAB_ACCESSION", $"{scope.BranchId}:{clock.UtcNow.Year}", $"ACC-{clock.UtcNow.Year}", ct);
        var updated = order with { Status = "Collected", Barcode = barcode, Accession = accession, CollectedAt = clock.UtcNow, SpecimenType = input.SpecimenType.Trim(), VolumeMl = input.VolumeMl, CollectedBy = scope.Actor.Id, Version = order.Version + 1 };
        await s.UpdateLabOrderAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "lab.specimen.collected", "LabOrder", id, changes: new { barcode, accession, input.SpecimenType, input.VolumeMl }), ct);
        return updated;
    }, ct);
    public Task<LabOrder> Reject(Scope scope, Guid id, VersionInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.LabCollect); await CareGuard.Module(s, "laboratory", ct);
        var order = await s.FindLabOrderAsync(id, ct) ?? throw new DomainException("not_found", "Lab order not found.", 404);
        CareGuard.Version(order.Version, input.Version);
        if (order.Status is not ("Ordered" or "Collected")) throw new DomainException("lab.state", "Only ordered or collected specimens can be rejected.", 409);
        var reason = CareGuard.Text(input.Reason, 5, 500, "Rejection reason");
        var updated = order with { Status = "Rejected", RejectionReason = reason, Version = order.Version + 1 };
        await s.UpdateLabOrderAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "lab.specimen.rejected", "LabOrder", id, reason), ct);
        return updated;
    }, ct);
    public Task<LabOrder> Process(Scope scope, Guid id, VersionInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.LabProcess); await CareGuard.Module(s, "laboratory", ct);
        var order = await s.FindLabOrderAsync(id, ct) ?? throw new DomainException("not_found", "Lab order not found.", 404);
        CareGuard.Version(order.Version, input.Version);
        if (order.Status != "Collected") throw new DomainException("lab.state", "Only collected specimens can enter processing.", 409);
        var updated = order with { Status = "Processing", ProcessedAt = clock.UtcNow, Version = order.Version + 1 };
        await s.UpdateLabOrderAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "lab.processing.started", "LabOrder", id), ct);
        return updated;
    }, ct);
    public Task<LabResult> EnterResult(Scope scope, Guid id, LabResultInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.LabResult); await CareGuard.Module(s, "laboratory", ct);
        var order = await s.FindLabOrderAsync(id, ct) ?? throw new DomainException("not_found", "Lab order not found.", 404);
        CareGuard.Version(order.Version, input.Version);
        if (order.Status is not ("Processing" or "Resulted")) throw new DomainException("lab.state", "Results require a processing specimen.", 409);
        var test = await s.FindLabTestAsync(order.TestId, ct) ?? throw new DomainException("lab.test", "Test not found.");
        var value = CareGuard.Text(input.Value, 1, 500, "Result value"); var unit = CareGuard.Text(input.Unit, 1, 40, "Unit");
        decimal? numeric = decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? n : null;
        var low = input.ReferenceLow ?? test.ReferenceLow; var high = input.ReferenceHigh ?? test.ReferenceHigh;
        if (low.HasValue && high.HasValue && low > high) throw new DomainException("lab.range", "Reference low cannot exceed high.");
        var flag = "Normal";
        if (numeric.HasValue)
        {
            if (test.CriticalLow.HasValue && numeric < test.CriticalLow) flag = "Critical low";
            else if (test.CriticalHigh.HasValue && numeric > test.CriticalHigh) flag = "Critical high";
            else if (low.HasValue && numeric < low) flag = "Low";
            else if (high.HasValue && numeric > high) flag = "High";
        }
        else flag = "Text";
        var previous = await s.FindLatestLabResultAsync(id, ct);
        var result = new LabResult(Guid.NewGuid(), id, (previous?.Version ?? 0) + 1, value, numeric, unit, low, high, flag, scope.Actor.Id, clock.UtcNow, null, null, null, null, null);
        await s.InsertLabResultAsync(result, ct);
        await s.UpdateLabOrderAsync(order with { Status = "Resulted", ResultedAt = clock.UtcNow, Version = order.Version + 1 }, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "lab.result.entered", "LabResult", result.Id, changes: new { order.Number, result.Version, flag }), ct);
        if (flag.StartsWith("Critical"))
        {
            var alert = new CriticalAlert(Guid.NewGuid(), "Laboratory", id, order.PatientId, $"{test.Name}: {value} {unit} ({flag})", "Critical", "Open", clock.UtcNow, null, null, "");
            await s.InsertCriticalAlertAsync(alert, ct);
            await s.AddOutboxAsync("lab.critical", alert.Id, JsonSerializer.Serialize(new { alert.Id, alert.PatientId, alert.Message }), ct);
        }
        return result;
    }, ct);
    public Task<LabResult> Validate(Scope scope, Guid id, VersionInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.LabValidate); await CareGuard.Module(s, "laboratory", ct);
        var order = await s.FindLabOrderAsync(id, ct) ?? throw new DomainException("not_found", "Lab order not found.", 404);
        CareGuard.Version(order.Version, input.Version);
        if (order.Status != "Resulted") throw new DomainException("lab.state", "Only resulted orders can be validated.", 409);
        var result = await s.FindLatestLabResultAsync(id, ct) ?? throw new DomainException("lab.result", "No result entered.");
        if (result.ValidatedAt is not null || result.EnteredBy == scope.Actor.Id) throw new DomainException("lab.separation", "Technical validation requires a different qualified staff member.", 409);
        var updated = result with { ValidatedBy = scope.Actor.Id, ValidatedAt = clock.UtcNow };
        await s.UpdateLabResultAsync(updated, ct);
        await s.UpdateLabOrderAsync(order with { Status = "Validated", ValidatedAt = clock.UtcNow, Version = order.Version + 1 }, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "lab.result.validated", "LabResult", result.Id), ct);
        return updated;
    }, ct);
    public Task<LabResult> Authorize(Scope scope, Guid id, VersionInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.LabAuthorize); await CareGuard.Module(s, "laboratory", ct);
        var order = await s.FindLabOrderAsync(id, ct) ?? throw new DomainException("not_found", "Lab order not found.", 404);
        CareGuard.Version(order.Version, input.Version);
        if (order.Status != "Validated") throw new DomainException("lab.state", "Only validated results can be authorized.", 409);
        var result = await s.FindLatestLabResultAsync(id, ct) ?? throw new DomainException("lab.result", "No result entered.");
        if (result.ValidatedAt is null || result.AuthorizedAt is not null || result.EnteredBy == scope.Actor.Id) throw new DomainException("lab.separation", "Authorization requires a pathologist distinct from the result entrant.", 409);
        var hash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { SchemaVersion = 2, order.Number, order.PatientId, order.TestId, order.Barcode, order.Accession, order.SpecimenType, order.VolumeMl, result.Value, result.Unit, result.ReferenceLow, result.ReferenceHigh, result.Flag, result.Version })))).ToLowerInvariant();
        var updated = result with { AuthorizedBy = scope.Actor.Id, AuthorizedAt = clock.UtcNow, ContentHash = hash };
        await s.UpdateLabResultAsync(updated, ct);
        await s.UpdateLabOrderAsync(order with { Status = "Authorized", AuthorizedAt = clock.UtcNow, Version = order.Version + 1 }, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "lab.result.authorized", "LabResult", result.Id, changes: new { hash }), ct);
        await s.AddOutboxAsync("lab.result.authorized", id, JsonSerializer.Serialize(new { OrderId = id, Hash = hash }), ct);
        return updated;
    }, ct);
    public Task<CriticalAlert> AcknowledgeAlert(Scope scope, Guid id, AlertAcknowledgeInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        var alert = await s.FindCriticalAlertAsync(id, ct) ?? throw new DomainException("not_found", "Alert not found.", 404);
        scope.Require(alert.Kind switch { "Nursing" => Permissions.NursingWrite, "Radiology" => Permissions.RadiologyRead, "Blood" => Permissions.BloodRead, _ => Permissions.LabAcknowledge });
        if (alert.Status != "Open") throw new DomainException("alert.state", "Only open alerts can be acknowledged.", 409);
        var note = CareGuard.Text(input.Note, 5, 1000, "Acknowledgement note");
        var updated = alert with { Status = "Acknowledged", AcknowledgedAt = clock.UtcNow, AcknowledgedBy = scope.Actor.Id, AcknowledgeNote = note };
        await s.UpdateCriticalAlertAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "alert.acknowledged", "CriticalAlert", id, note), ct);
        return updated;
    }, ct);
}
