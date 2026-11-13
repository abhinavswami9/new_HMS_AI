using Careflow.Application;
using Careflow.Domain;
namespace Careflow.Infrastructure;

public sealed partial class MemoryHospitalStore
{
    private static void CareVersion(long old, long next) { if (old != next - 1) throw new DomainException("concurrency", "Record version changed. Refresh before retrying.", 409); }
    public Task<CareWorkspace> LoadCareAsync(Scope s, int limit, CancellationToken ct)
    {
        var p = s.Actor.Permissions;
        List<T> Take<T>(IEnumerable<T> source) => source.Take(limit).ToList();
        var ipd = p.Contains(Permissions.IpdRead); var nursing = p.Contains(Permissions.NursingRead); var pharmacy = p.Contains(Permissions.PharmacyRead); var lab = p.Contains(Permissions.LabRead);
        var care = new CareWorkspace(
            ipd ? Take(data.Beds.OrderBy(b => b.Ward).ThenBy(b => b.Number)) : [],
            ipd ? Take(data.Admissions.OrderByDescending(a => a.AdmittedAt)) : [],
            ipd ? Take(data.BedEvents.OrderByDescending(e => e.At)) : [],
            nursing ? Take(data.NursingNotes.OrderByDescending(n => n.CreatedAt)) : [],
            nursing ? Take(data.MedicationOrders.OrderByDescending(o => o.CreatedAt)) : [],
            nursing ? Take(data.Administrations.OrderByDescending(a => a.GivenAt)) : [],
            nursing ? Take(data.Handovers.OrderByDescending(h => h.CreatedAt)) : [],
            pharmacy ? Take(data.StockBatches.OrderBy(b => b.ExpiryDate)) : [],
            pharmacy ? Take(data.StockMovements.OrderByDescending(m => m.At)) : [],
            pharmacy ? Take(data.Dispenses.OrderByDescending(d => d.CreatedAt)) : [],
            lab ? Take(data.LabTests.OrderBy(t => t.Name)) : [],
            lab ? Take(data.LabOrders.OrderByDescending(o => o.OrderedAt)) : [],
            lab ? Take(data.LabResults.OrderByDescending(r => r.EnteredAt)) : [],
            Take(data.CriticalAlerts.Where(a => a.Kind == "Nursing" ? nursing : lab).OrderByDescending(a => a.CreatedAt)),
            new[] { data.Beds.Count, data.Admissions.Count, data.BedEvents.Count, data.NursingNotes.Count, data.MedicationOrders.Count, data.Administrations.Count, data.Handovers.Count, data.StockBatches.Count, data.StockMovements.Count, data.Dispenses.Count, data.LabTests.Count, data.LabOrders.Count, data.LabResults.Count, data.CriticalAlerts.Count }.Any(n => n > limit));
        care = care with { Nurses = nursing ? LoadNursingStaffAsync(ct).Result : [] };
        return Task.FromResult(care);
    }
    public Task<bool> HasActiveAdmissionsAsync(CancellationToken ct) => Task.FromResult(data.Admissions.Any(a => a.Status == "Active"));
    public Task<Bed?> FindBedAsync(Guid id, CancellationToken ct) => Task.FromResult(data.Beds.FirstOrDefault(b => b.Id == id));
    public Task UpdateBedAsync(Bed bed, CancellationToken ct) { var old = FindBedAsync(bed.Id, ct).Result ?? throw new DomainException("not_found", "Bed not found.", 404); CareVersion(old.Version, bed.Version); data.Beds[data.Beds.IndexOf(old)] = bed; return Task.CompletedTask; }
    public Task<Admission?> FindAdmissionAsync(Guid id, CancellationToken ct) => Task.FromResult(data.Admissions.FirstOrDefault(a => a.Id == id));
    public Task<Admission?> FindActiveAdmissionByPatientAsync(Guid patientId, CancellationToken ct) => Task.FromResult(data.Admissions.FirstOrDefault(a => a.PatientId == patientId && a.Status == "Active"));
    public Task<Admission?> FindActiveAdmissionByBedAsync(Guid bedId, CancellationToken ct) => Task.FromResult(data.Admissions.FirstOrDefault(a => a.BedId == bedId && a.Status == "Active"));
    public Task InsertAdmissionAsync(Admission admission, CancellationToken ct) { data.Admissions.Add(admission); return Task.CompletedTask; }
    public Task UpdateAdmissionAsync(Admission admission, CancellationToken ct) { var old = FindAdmissionAsync(admission.Id, ct).Result ?? throw new DomainException("not_found", "Admission not found.", 404); CareVersion(old.Version, admission.Version); if (old.Status != "Active") throw new DomainException("admission.immutable", "Discharged admissions are immutable.", 409); data.Admissions[data.Admissions.IndexOf(old)] = admission; return Task.CompletedTask; }
    public Task InsertBedEventAsync(BedEvent bedEvent, CancellationToken ct) { data.BedEvents.Add(bedEvent); return Task.CompletedTask; }
    public Task InsertNursingNoteAsync(NursingNote note, CancellationToken ct) { data.NursingNotes.Add(note); return Task.CompletedTask; }
    public Task<MedicationOrder?> FindMedicationOrderAsync(Guid id, CancellationToken ct) => Task.FromResult(data.MedicationOrders.FirstOrDefault(o => o.Id == id));
    public Task InsertMedicationOrderAsync(MedicationOrder order, CancellationToken ct) { data.MedicationOrders.Add(order); return Task.CompletedTask; }
    public Task UpdateMedicationOrderAsync(MedicationOrder order, CancellationToken ct) { var old = FindMedicationOrderAsync(order.Id, ct).Result ?? throw new DomainException("not_found", "Medication order not found.", 404); CareVersion(old.Version, order.Version); if (old.Status != "Active") throw new DomainException("medication.immutable", "Stopped orders are immutable.", 409); data.MedicationOrders[data.MedicationOrders.IndexOf(old)] = order; return Task.CompletedTask; }
    public Task<bool> HasAdministrationAtAsync(Guid orderId, DateTimeOffset givenAt, CancellationToken ct) => Task.FromResult(data.Administrations.Any(a => a.OrderId == orderId && a.GivenAt == givenAt));
    public Task InsertAdministrationAsync(MedicationAdministration administration, CancellationToken ct) { data.Administrations.Add(administration); return Task.CompletedTask; }
    public Task<ShiftHandover?> FindHandoverAsync(Guid id, CancellationToken ct) => Task.FromResult(data.Handovers.FirstOrDefault(h => h.Id == id));
    public Task InsertHandoverAsync(ShiftHandover handover, CancellationToken ct) { data.Handovers.Add(handover); return Task.CompletedTask; }
    public Task UpdateHandoverAsync(ShiftHandover handover, CancellationToken ct) { var old = FindHandoverAsync(handover.Id, ct).Result ?? throw new DomainException("not_found", "Handover not found.", 404); CareVersion(old.Version, handover.Version); if (old.Status != "Pending") throw new DomainException("handover.immutable", "Acknowledged handovers are immutable.", 409); data.Handovers[data.Handovers.IndexOf(old)] = handover; return Task.CompletedTask; }
    public Task<StockBatch?> FindStockBatchAsync(Guid id, CancellationToken ct) => Task.FromResult(data.StockBatches.FirstOrDefault(b => b.Id == id));
    public Task<IReadOnlyList<StockBatch>> LoadAvailableBatchesAsync(Guid medicineId, DateOnly today, CancellationToken ct) => Task.FromResult<IReadOnlyList<StockBatch>>(data.StockBatches.Where(b => b.MedicineId == medicineId && b.Status == "Active" && b.Quantity > 0 && b.ExpiryDate > today).OrderBy(b => b.ExpiryDate).ThenBy(b => b.ReceivedAt).ToList());
    public Task InsertStockBatchAsync(StockBatch batch, CancellationToken ct) { if (data.StockBatches.Any(b => b.MedicineId == batch.MedicineId && b.BatchNumber == batch.BatchNumber && b.Supplier == batch.Supplier)) throw new DomainException("stock.duplicate", "This batch/reference already exists for the medicine and supplier.", 409); data.StockBatches.Add(batch); return Task.CompletedTask; }
    public Task UpdateStockBatchAsync(StockBatch batch, decimal expectedQuantity, CancellationToken ct) { var old = FindStockBatchAsync(batch.Id, ct).Result ?? throw new DomainException("not_found", "Stock batch not found.", 404); CareVersion(old.Version, batch.Version); if (old.Quantity != expectedQuantity || old.Status != "Active") throw new DomainException("concurrency", "Stock batch changed. Refresh before retrying.", 409); data.StockBatches[data.StockBatches.IndexOf(old)] = batch; return Task.CompletedTask; }
    public Task<IReadOnlyList<MedicationOrder>> LoadActiveMedicationOrdersAsync(Guid admissionId, CancellationToken ct) => Task.FromResult<IReadOnlyList<MedicationOrder>>(data.MedicationOrders.Where(o => o.AdmissionId == admissionId && o.Status == "Active").ToList());
    public Task<string?> FindPolicyAsync(string key, CancellationToken ct) => Task.FromResult(data.Policies.GetValueOrDefault(key));
    public Task<IReadOnlyList<StaffMember>> LoadNursingStaffAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<StaffMember>>(data.Users.Where(u => u.Enabled && (u.Grants ?? Permissions.For(u.Role)).Contains(Permissions.NursingHandover)).Select(u => new StaffMember(u.Id, u.DisplayName)).ToList());
    public Task InsertStockMovementAsync(StockMovement movement, CancellationToken ct) { data.StockMovements.Add(movement); return Task.CompletedTask; }
    public Task<Dispense?> FindDispenseByKeyAsync(string key, CancellationToken ct) => Task.FromResult(data.Dispenses.FirstOrDefault(d => d.IdempotencyKey == key && d.CreatedBy == scope.Actor.Id));
    public Task InsertDispenseAsync(Dispense dispense, CancellationToken ct) { if (data.Dispenses.Any(d => d.IdempotencyKey == dispense.IdempotencyKey && d.CreatedBy == dispense.CreatedBy)) throw new DomainException("dispense.idempotency", "This dispensing key was already used.", 409); data.Dispenses.Add(dispense); return Task.CompletedTask; }
    public Task<LabTest?> FindLabTestAsync(Guid id, CancellationToken ct) => Task.FromResult(data.LabTests.FirstOrDefault(t => t.Id == id));
    public Task<IReadOnlyList<LabTest>> LoadLabTestsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<LabTest>>(data.LabTests.Where(t => t.Active).OrderBy(t => t.Name).ToList());
    public Task<LabOrder?> FindLabOrderAsync(Guid id, CancellationToken ct) => Task.FromResult(data.LabOrders.FirstOrDefault(o => o.Id == id));
    public Task InsertLabOrderAsync(LabOrder order, CancellationToken ct) { data.LabOrders.Add(order); return Task.CompletedTask; }
    public Task UpdateLabOrderAsync(LabOrder order, CancellationToken ct) { var old = FindLabOrderAsync(order.Id, ct).Result ?? throw new DomainException("not_found", "Lab order not found.", 404); CareVersion(old.Version, order.Version); if (old.Status is "Authorized" or "Rejected") throw new DomainException("lab.immutable", "Authorized/rejected lab orders are immutable.", 409); data.LabOrders[data.LabOrders.IndexOf(old)] = order; return Task.CompletedTask; }
    public Task<LabResult?> FindLatestLabResultAsync(Guid orderId, CancellationToken ct) => Task.FromResult(data.LabResults.Where(r => r.OrderId == orderId).OrderByDescending(r => r.Version).FirstOrDefault());
    public Task InsertLabResultAsync(LabResult result, CancellationToken ct) { data.LabResults.Add(result); return Task.CompletedTask; }
    public Task UpdateLabResultAsync(LabResult result, CancellationToken ct) { var old = data.LabResults.FirstOrDefault(r => r.Id == result.Id) ?? throw new DomainException("not_found", "Lab result not found.", 404); if (old.AuthorizedAt is not null) throw new DomainException("lab.immutable", "Authorized results are immutable.", 409); data.LabResults[data.LabResults.IndexOf(old)] = result; return Task.CompletedTask; }
    public Task<CriticalAlert?> FindCriticalAlertAsync(Guid id, CancellationToken ct) => Task.FromResult(data.CriticalAlerts.FirstOrDefault(a => a.Id == id));
    public Task InsertCriticalAlertAsync(CriticalAlert alert, CancellationToken ct) { data.CriticalAlerts.Add(alert); return Task.CompletedTask; }
    public Task UpdateCriticalAlertAsync(CriticalAlert alert, CancellationToken ct) { var old = FindCriticalAlertAsync(alert.Id, ct).Result ?? throw new DomainException("not_found", "Alert not found.", 404); if (old.Status != "Open") throw new DomainException("alert.immutable", "Acknowledged alerts are immutable.", 409); data.CriticalAlerts[data.CriticalAlerts.IndexOf(old)] = alert; return Task.CompletedTask; }

    public Task<CareChart> LoadCareChartAsync(Guid patientId, int limit, CancellationToken ct)
    {
        var p = scope.Actor.Permissions;
        List<T> Take<T>(IEnumerable<T> source) => source.Take(limit).ToList();
        var ipd = p.Contains(Permissions.IpdRead); var nursing = p.Contains(Permissions.NursingRead); var pharmacy = p.Contains(Permissions.PharmacyRead); var lab = p.Contains(Permissions.LabRead);
        var admissions = ipd ? Take(data.Admissions.Where(a => a.PatientId == patientId).OrderByDescending(a => a.AdmittedAt)) : [];
        var notes = nursing ? Take(data.NursingNotes.Where(n => n.PatientId == patientId).OrderByDescending(n => n.CreatedAt)) : [];
        var orders = nursing ? Take(data.MedicationOrders.Where(o => o.PatientId == patientId).OrderByDescending(o => o.CreatedAt)) : [];
        var dispenses = pharmacy ? Take(data.Dispenses.Where(d => d.PatientId == patientId).OrderByDescending(d => d.CreatedAt)) : [];
        var labOrders = lab ? Take(data.LabOrders.Where(o => o.PatientId == patientId).OrderByDescending(o => o.OrderedAt)) : [];
        var patientOrderIds = data.LabOrders.Where(o => o.PatientId == patientId).Select(o => o.Id).ToHashSet();
        var labResults = lab ? Take(data.LabResults.Where(r => patientOrderIds.Contains(r.OrderId)).OrderByDescending(r => r.EnteredAt)) : [];
        var truncated = new[] { data.Admissions.Count(a => a.PatientId == patientId), data.NursingNotes.Count(n => n.PatientId == patientId), data.MedicationOrders.Count(o => o.PatientId == patientId), data.Dispenses.Count(d => d.PatientId == patientId), data.LabOrders.Count(o => o.PatientId == patientId) }.Any(n => n > limit);
        return Task.FromResult(new CareChart(admissions, notes, orders, dispenses, labOrders, labResults, truncated));
    }
}
