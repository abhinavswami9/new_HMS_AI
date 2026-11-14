using Careflow.Application;
using Careflow.Domain;
namespace Careflow.Infrastructure;

// Partial MemoryHospitalStore slice for the Phase 4 packages: insurance/TPA,
// doctor incentives, telemedicine, inventory/procurement and CSSD.

public sealed partial class MemoryHospitalStore
{
    // ---- Phase 4: insurance -------------------------------------------------
    public Task<Coverage?> FindCoverageAsync(Guid id, CancellationToken ct) => Task.FromResult(data.Coverages.FirstOrDefault(x => x.Id == id));
    public Task<Coverage?> FindCoverageByPolicyAsync(Guid patientId, string policyNumber, CancellationToken ct) =>
        Task.FromResult(data.Coverages.FirstOrDefault(x => x.PatientId == patientId && x.PolicyNumber == policyNumber));
    public Task InsertCoverageAsync(Coverage coverage, CancellationToken ct) { data.Coverages.Add(coverage); return Task.CompletedTask; }
    public Task UpdateCoverageAsync(Coverage coverage, CancellationToken ct) { var old = data.Coverages.FirstOrDefault(x => x.Id == coverage.Id) ?? throw new DomainException("not_found", "Coverage not found.", 404); CareVersion(old.Version, coverage.Version); data.Coverages[data.Coverages.IndexOf(old)] = coverage; return Task.CompletedTask; }
    public Task<IReadOnlyList<Coverage>> ListCoveragesAsync(Guid? patientId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Coverage>>(data.Coverages.Where(x => patientId is null || x.PatientId == patientId).ToList());
    public Task<IReadOnlyList<Payer>> ListPayersAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Payer>>(data.Payers.ToList());
    public Task<InsurancePlan?> FindInsurancePlanAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(data.InsurancePlans.FirstOrDefault(x => x.Id == id));
    public Task InsertInsurancePlanAsync(InsurancePlan plan, CancellationToken ct) { data.InsurancePlans.Add(plan); return Task.CompletedTask; }
    public Task<Preauthorization?> FindPreauthAsync(Guid id, CancellationToken ct) => Task.FromResult(data.Preauthorizations.FirstOrDefault(x => x.Id == id));
    public Task InsertPreauthAsync(Preauthorization preauth, CancellationToken ct) { data.Preauthorizations.Add(preauth); return Task.CompletedTask; }
    public Task UpdatePreauthAsync(Preauthorization preauth, CancellationToken ct) { var old = data.Preauthorizations.FirstOrDefault(x => x.Id == preauth.Id) ?? throw new DomainException("not_found", "Preauthorization not found.", 404); CareVersion(old.Version, preauth.Version); if (old.Status is "Approved" or "Rejected") throw new DomainException("insurance.immutable", "Answered preauthorizations are immutable.", 409); data.Preauthorizations[data.Preauthorizations.IndexOf(old)] = preauth; return Task.CompletedTask; }
    public Task<IReadOnlyList<Preauthorization>> ListPreauthsAsync(Guid? patientId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Preauthorization>>(data.Preauthorizations.Where(x => patientId is null || x.PatientId == patientId).ToList());
    public Task<Claim?> FindClaimAsync(Guid id, CancellationToken ct) => Task.FromResult(data.Claims.FirstOrDefault(x => x.Id == id));
    public Task<Claim?> FindClaimByInvoiceAsync(Guid invoiceId, CancellationToken ct) =>
        Task.FromResult(data.Claims.FirstOrDefault(x => x.InvoiceId == invoiceId));
    public Task InsertClaimAsync(Claim claim, CancellationToken ct) { data.Claims.Add(claim); return Task.CompletedTask; }
    public Task UpdateClaimAsync(Claim claim, CancellationToken ct) { var old = data.Claims.FirstOrDefault(x => x.Id == claim.Id) ?? throw new DomainException("not_found", "Claim not found.", 404); CareVersion(old.Version, claim.Version); if (old.Status is "Settled" or "Rejected") throw new DomainException("insurance.immutable", "Settled or rejected claims are immutable.", 409); data.Claims[data.Claims.IndexOf(old)] = claim; return Task.CompletedTask; }
    public Task<IReadOnlyList<Claim>> ListClaimsAsync(Guid? patientId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Claim>>(data.Claims.Where(x => patientId is null || x.PatientId == patientId).ToList());
    public Task InsertClaimLineAsync(ClaimLine line, CancellationToken ct) { data.ClaimLines.Add(line); return Task.CompletedTask; }
    public Task<IReadOnlyList<ClaimLine>> ListClaimLinesAsync(Guid claimId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ClaimLine>>(data.ClaimLines.Where(x => x.ClaimId == claimId).ToList());
    public Task InsertClaimQueryAsync(ClaimQuery query, CancellationToken ct) { data.ClaimQueries.Add(query); return Task.CompletedTask; }
    public Task UpdateClaimQueryAsync(ClaimQuery query, CancellationToken ct) { var old = data.ClaimQueries.FirstOrDefault(x => x.Id == query.Id) ?? throw new DomainException("not_found", "Claim query not found.", 404); data.ClaimQueries[data.ClaimQueries.IndexOf(old)] = query; return Task.CompletedTask; }
    public Task<IReadOnlyList<ClaimQuery>> ListClaimQueriesAsync(Guid claimId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ClaimQuery>>(data.ClaimQueries.Where(x => x.ClaimId == claimId).ToList());
    public Task<Settlement?> FindSettlementByReferenceAsync(string reference, CancellationToken ct) =>
        Task.FromResult(data.Settlements.FirstOrDefault(x => x.Reference == reference));
    public Task InsertSettlementAsync(Settlement settlement, CancellationToken ct) { data.Settlements.Add(settlement); return Task.CompletedTask; }
    public Task<IReadOnlyList<Settlement>> ListSettlementsAsync(Guid claimId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Settlement>>(data.Settlements.Where(x => x.ClaimId == claimId).ToList());

    // ---- Phase 4: incentives ------------------------------------------------
    public Task<IncentiveRule?> FindIncentiveRuleAsync(Guid id, CancellationToken ct) => Task.FromResult(data.IncentiveRules.FirstOrDefault(x => x.Id == id));
    public Task InsertIncentiveRuleAsync(IncentiveRule rule, CancellationToken ct) { data.IncentiveRules.Add(rule); return Task.CompletedTask; }
    public Task UpdateIncentiveRuleAsync(IncentiveRule rule, CancellationToken ct) { var old = data.IncentiveRules.FirstOrDefault(x => x.Id == rule.Id) ?? throw new DomainException("not_found", "Incentive rule not found.", 404); CareVersion(old.Version, rule.Version); data.IncentiveRules[data.IncentiveRules.IndexOf(old)] = rule; return Task.CompletedTask; }
    public Task<IReadOnlyList<IncentiveRule>> ListIncentiveRulesAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<IncentiveRule>>(data.IncentiveRules.ToList());
    public Task<bool> HasOverlappingIncentiveRuleAsync(string scopeKind, Guid? doctorId, string basis, DateOnly from, DateOnly? to, Guid? excludeId, CancellationToken ct) =>
        Task.FromResult(data.IncentiveRules.Any(r =>
            r.ScopeKind == scopeKind && r.DoctorId == doctorId && r.Basis == basis &&
            (excludeId is null || r.Id != excludeId) &&
            from <= (to ?? new DateOnly(2999, 12, 31)) &&
            (r.EffectiveTo ?? new DateOnly(2999, 12, 31)) >= from));
    public Task<IReadOnlyList<IncentiveRule>> ListActiveIncentiveRulesAsync(DateOnly on, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<IncentiveRule>>(data.IncentiveRules.Where(r =>
            r.Status == "Active" && r.EffectiveFrom <= on && (r.EffectiveTo is null || r.EffectiveTo >= on)).ToList());
    public Task<IncentiveBatch?> FindIncentiveBatchAsync(Guid id, CancellationToken ct) => Task.FromResult(data.IncentiveBatches.FirstOrDefault(x => x.Id == id));
    public Task<IncentiveBatch?> FindIncentiveBatchByPeriodAsync(DateOnly from, DateOnly to, CancellationToken ct) =>
        Task.FromResult(data.IncentiveBatches.FirstOrDefault(x => x.PeriodStart == from && x.PeriodEnd == to));
    public Task InsertIncentiveBatchAsync(IncentiveBatch batch, CancellationToken ct) { data.IncentiveBatches.Add(batch); return Task.CompletedTask; }
    public Task UpdateIncentiveBatchAsync(IncentiveBatch batch, CancellationToken ct) { var old = data.IncentiveBatches.FirstOrDefault(x => x.Id == batch.Id) ?? throw new DomainException("not_found", "Incentive batch not found.", 404); CareVersion(old.Version, batch.Version); if (old.Status == "Paid") throw new DomainException("incentive.immutable", "Paid batches are immutable.", 409); data.IncentiveBatches[data.IncentiveBatches.IndexOf(old)] = batch; return Task.CompletedTask; }
    public Task<IReadOnlyList<IncentiveBatch>> ListIncentiveBatchesAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<IncentiveBatch>>(data.IncentiveBatches.ToList());
    public Task InsertIncentiveLineAsync(IncentiveLine line, CancellationToken ct) { data.IncentiveLines.Add(line); return Task.CompletedTask; }
    public Task DeleteIncentiveLinesAsync(Guid batchId, CancellationToken ct) { data.IncentiveLines.RemoveAll(x => x.BatchId == batchId); return Task.CompletedTask; }
    public Task<IReadOnlyList<IncentiveLine>> ListIncentiveLinesAsync(Guid batchId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<IncentiveLine>>(data.IncentiveLines.Where(x => x.BatchId == batchId).ToList());
    public Task<IReadOnlyList<IncentiveEvent>> ListIncentiveEventsAsync(DateOnly from, DateOnly to, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<IncentiveEvent>>(BuildIncentiveEvents(from, to));
    public Task InsertIncentivePaymentAsync(IncentivePayment payment, CancellationToken ct) { data.IncentivePayments.Add(payment); return Task.CompletedTask; }
    public Task<IReadOnlyList<IncentivePayment>> ListIncentivePaymentsAsync(Guid batchId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<IncentivePayment>>(data.IncentivePayments.Where(x => x.BatchId == batchId).ToList());

    private List<IncentiveEvent> BuildIncentiveEvents(DateOnly from, DateOnly to)
    {
        bool InRange(DateOnly d) => d >= from && d <= to;
        var events = new List<IncentiveEvent>();
        foreach (var encounter in data.Encounters.Where(e => e.Status == "Signed" && InRange(DateOnly.FromDateTime(e.UpdatedAt.UtcDateTime))))
            events.Add(new IncentiveEvent("Consultation", encounter.DoctorId, encounter.Id.ToString("N"), 0m, encounter.UpdatedAt));
        foreach (var invoice in data.Invoices)
            foreach (var payment in invoice.Payments.Where(p => InRange(DateOnly.FromDateTime(p.ReceivedAt.UtcDateTime))))
            {
                var doctorId = data.Encounters.Where(e => e.PatientId == invoice.PatientId && e.Status == "Signed" && e.UpdatedAt <= payment.ReceivedAt)
                    .OrderByDescending(e => e.UpdatedAt).Select(e => (Guid?)e.DoctorId).FirstOrDefault();
                if (doctorId is null) continue;
                events.Add(new IncentiveEvent("Revenue", doctorId.Value, payment.Id.ToString("N"), payment.Amount, payment.ReceivedAt));
            }
        foreach (var booking in data.OtBookings.Where(b => b.Status == "Completed" && b.ScheduledStart is { } start && InRange(DateOnly.FromDateTime(start.UtcDateTime))))
            events.Add(new IncentiveEvent("Surgery", booking.SurgeonId, booking.Id.ToString("N"), 0m, booking.ScheduledStart!.Value));
        foreach (var report in data.RadiologyReports.Where(r => r.AuthorizedAt is { } at && InRange(DateOnly.FromDateTime(at.UtcDateTime))))
        {
            var order = data.RadiologyOrders.FirstOrDefault(o => o.Id == report.OrderId);
            if (order is null) continue;
            var doctorId = data.Encounters.Where(e => e.PatientId == order.PatientId && e.Status == "Signed" && e.UpdatedAt <= report.AuthorizedAt)
                .OrderByDescending(e => e.UpdatedAt).Select(e => (Guid?)e.DoctorId).FirstOrDefault();
            if (doctorId is null) continue;
            events.Add(new IncentiveEvent("Procedure", doctorId.Value, report.Id.ToString("N"), 0m, report.AuthorizedAt!.Value));
        }
        return events;
    }

    // ---- Phase 4: telemedicine ---------------------------------------------
    public Task<TeleSession?> FindTeleSessionAsync(Guid id, CancellationToken ct) => Task.FromResult(data.TeleSessions.FirstOrDefault(x => x.Id == id));
    public Task<TeleSession?> FindTeleSessionByAppointmentAsync(Guid appointmentId, CancellationToken ct) =>
        Task.FromResult(data.TeleSessions.FirstOrDefault(x => x.AppointmentId == appointmentId));
    public Task InsertTeleSessionAsync(TeleSession session, CancellationToken ct) { data.TeleSessions.Add(session); return Task.CompletedTask; }
    public Task UpdateTeleSessionAsync(TeleSession session, CancellationToken ct) { var old = data.TeleSessions.FirstOrDefault(x => x.Id == session.Id) ?? throw new DomainException("not_found", "Tele session not found.", 404); CareVersion(old.Version, session.Version); if (old.Status is "Completed" or "Cancelled") throw new DomainException("tele.immutable", "Closed tele sessions are immutable.", 409); data.TeleSessions[data.TeleSessions.IndexOf(old)] = session; return Task.CompletedTask; }
    public Task<IReadOnlyList<TeleSession>> ListTeleSessionsAsync(Guid? patientId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<TeleSession>>(data.TeleSessions.Where(x => patientId is null || x.PatientId == patientId).ToList());
    public Task InsertTeleConsentEventAsync(TeleConsentEvent evt, CancellationToken ct) { data.TeleConsentEvents.Add(evt); return Task.CompletedTask; }
    public Task<IReadOnlyList<TeleConsentEvent>> ListTeleConsentEventsAsync(Guid sessionId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<TeleConsentEvent>>(data.TeleConsentEvents.Where(x => x.SessionId == sessionId).ToList());
    public Task InsertTeleWaitingEventAsync(TeleWaitingEvent evt, CancellationToken ct) { data.TeleWaitingEvents.Add(evt); return Task.CompletedTask; }
    public Task<IReadOnlyList<TeleWaitingEvent>> ListTeleWaitingEventsAsync(Guid sessionId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<TeleWaitingEvent>>(data.TeleWaitingEvents.Where(x => x.SessionId == sessionId).ToList());
    public Task InsertTeleUploadAsync(TeleUpload upload, CancellationToken ct) { data.TeleUploads.Add(upload); return Task.CompletedTask; }
    public Task<IReadOnlyList<TeleUpload>> ListTeleUploadsAsync(Guid sessionId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<TeleUpload>>(data.TeleUploads.Where(x => x.SessionId == sessionId).ToList());

    // ---- Phase 4: inventory ------------------------------------------------
    public Task<InventoryStore?> FindInventoryStoreAsync(Guid id, CancellationToken ct) => Task.FromResult(data.InventoryStores.FirstOrDefault(x => x.Id == id));
    public Task InsertInventoryStoreAsync(InventoryStore store, CancellationToken ct) { data.InventoryStores.Add(store); return Task.CompletedTask; }
    public Task<IReadOnlyList<InventoryStore>> ListInventoryStoresAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<InventoryStore>>(data.InventoryStores.ToList());
    public Task<InventoryItem?> FindInventoryItemAsync(Guid id, CancellationToken ct) => Task.FromResult(data.InventoryItems.FirstOrDefault(x => x.Id == id));
    public Task InsertInventoryItemAsync(InventoryItem item, CancellationToken ct) { data.InventoryItems.Add(item); return Task.CompletedTask; }
    public Task<IReadOnlyList<InventoryItem>> ListInventoryItemsAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<InventoryItem>>(data.InventoryItems.ToList());
    public Task<InventoryBatch?> FindInventoryBatchAsync(Guid id, CancellationToken ct) => Task.FromResult(data.InventoryBatches.FirstOrDefault(x => x.Id == id));
    public Task<InventoryBatch?> FindInventoryBatchByNumberAsync(Guid itemId, string batchNumber, Guid storeId, CancellationToken ct) =>
        Task.FromResult(data.InventoryBatches.FirstOrDefault(x => x.ItemId == itemId && x.BatchNumber == batchNumber && x.StoreId == storeId));
    public Task InsertInventoryBatchAsync(InventoryBatch batch, CancellationToken ct) { data.InventoryBatches.Add(batch); return Task.CompletedTask; }
    public Task UpdateInventoryBatchAsync(InventoryBatch batch, CancellationToken ct) { var old = data.InventoryBatches.FirstOrDefault(x => x.Id == batch.Id) ?? throw new DomainException("not_found", "Batch not found.", 404); CareVersion(old.Version, batch.Version); data.InventoryBatches[data.InventoryBatches.IndexOf(old)] = batch; return Task.CompletedTask; }
    public Task<IReadOnlyList<InventoryBatch>> ListInventoryBatchesAsync(Guid? storeId, Guid? itemId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<InventoryBatch>>(data.InventoryBatches.Where(x => (storeId is null || x.StoreId == storeId) && (itemId is null || x.ItemId == itemId)).ToList());
    public Task InsertInventoryMovementAsync(InventoryMovement movement, CancellationToken ct) { data.InventoryMovements.Add(movement); return Task.CompletedTask; }
    public Task<IReadOnlyList<InventoryMovement>> ListInventoryMovementsAsync(Guid? batchId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<InventoryMovement>>(data.InventoryMovements.Where(x => batchId is null || x.BatchId == batchId).ToList());
    public Task<Vendor?> FindVendorByNameAsync(string name, CancellationToken ct) =>
        Task.FromResult(data.Vendors.FirstOrDefault(x => x.Name == name));
    public Task InsertVendorAsync(Vendor vendor, CancellationToken ct) { data.Vendors.Add(vendor); return Task.CompletedTask; }
    public Task<IReadOnlyList<Vendor>> ListVendorsAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Vendor>>(data.Vendors.ToList());
    public Task<InventoryIndent?> FindInventoryIndentAsync(Guid id, CancellationToken ct) => Task.FromResult(data.InventoryIndents.FirstOrDefault(x => x.Id == id));
    public Task InsertInventoryIndentAsync(InventoryIndent indent, CancellationToken ct) { data.InventoryIndents.Add(indent); return Task.CompletedTask; }
    public Task UpdateInventoryIndentAsync(InventoryIndent indent, CancellationToken ct) { var old = data.InventoryIndents.FirstOrDefault(x => x.Id == indent.Id) ?? throw new DomainException("not_found", "Indent not found.", 404); CareVersion(old.Version, indent.Version); if (old.Status is "Rejected" or "Received") throw new DomainException("inventory.immutable", "Closed indents are immutable.", 409); data.InventoryIndents[data.InventoryIndents.IndexOf(old)] = indent; return Task.CompletedTask; }
    public Task<IReadOnlyList<InventoryIndent>> ListInventoryIndentsAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<InventoryIndent>>(data.InventoryIndents.ToList());
    public Task<PurchaseOrder?> FindPurchaseOrderAsync(Guid id, CancellationToken ct) => Task.FromResult(data.PurchaseOrders.FirstOrDefault(x => x.Id == id));
    public Task<PurchaseOrder?> FindPurchaseOrderByIndentAsync(Guid indentId, CancellationToken ct) =>
        Task.FromResult(data.PurchaseOrders.FirstOrDefault(x => x.IndentId == indentId));
    public Task InsertPurchaseOrderAsync(PurchaseOrder order, CancellationToken ct) { data.PurchaseOrders.Add(order); return Task.CompletedTask; }
    public Task UpdatePurchaseOrderAsync(PurchaseOrder order, CancellationToken ct) { var old = data.PurchaseOrders.FirstOrDefault(x => x.Id == order.Id) ?? throw new DomainException("not_found", "Purchase order not found.", 404); CareVersion(old.Version, order.Version); if (old.Status == "Received") throw new DomainException("inventory.immutable", "Received purchase orders are immutable.", 409); data.PurchaseOrders[data.PurchaseOrders.IndexOf(old)] = order; return Task.CompletedTask; }
    public Task<IReadOnlyList<PurchaseOrder>> ListPurchaseOrdersAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<PurchaseOrder>>(data.PurchaseOrders.ToList());
    public Task<GoodsReceipt?> FindGoodsReceiptByPoAsync(Guid poId, CancellationToken ct) =>
        Task.FromResult(data.GoodsReceipts.FirstOrDefault(x => x.PurchaseOrderId == poId));
    public Task InsertGoodsReceiptAsync(GoodsReceipt receipt, CancellationToken ct) { data.GoodsReceipts.Add(receipt); return Task.CompletedTask; }
    public Task<InventoryTransfer?> FindTransferAsync(Guid id, CancellationToken ct) => Task.FromResult(data.InventoryTransfers.FirstOrDefault(x => x.Id == id));
    public Task InsertTransferAsync(InventoryTransfer transfer, CancellationToken ct) { data.InventoryTransfers.Add(transfer); return Task.CompletedTask; }
    public Task UpdateTransferAsync(InventoryTransfer transfer, CancellationToken ct) { var old = data.InventoryTransfers.FirstOrDefault(x => x.Id == transfer.Id) ?? throw new DomainException("not_found", "Transfer not found.", 404); CareVersion(old.Version, transfer.Version); if (old.Status == "Received") throw new DomainException("inventory.immutable", "Received transfers are immutable.", 409); data.InventoryTransfers[data.InventoryTransfers.IndexOf(old)] = transfer; return Task.CompletedTask; }
    public Task<IReadOnlyList<InventoryTransfer>> ListTransfersAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<InventoryTransfer>>(data.InventoryTransfers.ToList());
    public Task InsertConsumptionAsync(InventoryConsumption consumption, CancellationToken ct) { data.InventoryConsumptions.Add(consumption); return Task.CompletedTask; }
    public Task<IReadOnlyList<InventoryConsumption>> ListConsumptionsAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<InventoryConsumption>>(data.InventoryConsumptions.ToList());
    public Task<Asset?> FindAssetBySerialAsync(string serial, CancellationToken ct) =>
        Task.FromResult(data.Assets.FirstOrDefault(x => x.SerialNumber == serial));
    public Task InsertAssetAsync(Asset asset, CancellationToken ct) { data.Assets.Add(asset); return Task.CompletedTask; }
    public Task<IReadOnlyList<Asset>> ListAssetsAsync(Guid? itemId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Asset>>(data.Assets.Where(x => itemId is null || x.ItemId == itemId).ToList());

    // ---- Phase 4: CSSD -----------------------------------------------------
    public Task<InstrumentSet?> FindInstrumentSetAsync(Guid id, CancellationToken ct) => Task.FromResult(data.InstrumentSets.FirstOrDefault(x => x.Id == id));
    public Task InsertInstrumentSetAsync(InstrumentSet set, CancellationToken ct) { data.InstrumentSets.Add(set); return Task.CompletedTask; }
    public Task UpdateInstrumentSetAsync(InstrumentSet set, CancellationToken ct) { var old = data.InstrumentSets.FirstOrDefault(x => x.Id == set.Id) ?? throw new DomainException("not_found", "Instrument set not found.", 404); CareVersion(old.Version, set.Version); data.InstrumentSets[data.InstrumentSets.IndexOf(old)] = set; return Task.CompletedTask; }
    public Task<IReadOnlyList<InstrumentSet>> ListInstrumentSetsAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<InstrumentSet>>(data.InstrumentSets.ToList());
    public Task InsertInstrumentItemAsync(InstrumentItem item, CancellationToken ct) { data.InstrumentItems.Add(item); return Task.CompletedTask; }
    public Task<IReadOnlyList<InstrumentItem>> ListInstrumentItemsAsync(Guid setId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<InstrumentItem>>(data.InstrumentItems.Where(x => x.SetId == setId).ToList());
    public Task<SterilizerCycle?> FindCycleAsync(Guid id, CancellationToken ct) => Task.FromResult(data.SterilizerCycles.FirstOrDefault(x => x.Id == id));
    public Task InsertCycleAsync(SterilizerCycle cycle, CancellationToken ct) { data.SterilizerCycles.Add(cycle); return Task.CompletedTask; }
    public Task UpdateCycleAsync(SterilizerCycle cycle, CancellationToken ct) { var old = data.SterilizerCycles.FirstOrDefault(x => x.Id == cycle.Id) ?? throw new DomainException("not_found", "Cycle not found.", 404); CareVersion(old.Version, cycle.Version); if (old.Status is "Passed" or "Failed") throw new DomainException("cssd.immutable", "Closed cycles are immutable.", 409); data.SterilizerCycles[data.SterilizerCycles.IndexOf(old)] = cycle; return Task.CompletedTask; }
    public Task<IReadOnlyList<SterilizerCycle>> ListCyclesAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<SterilizerCycle>>(data.SterilizerCycles.ToList());
    public Task InsertCycleLoadAsync(CycleLoad load, CancellationToken ct) { data.CycleLoads.Add(load); return Task.CompletedTask; }
    public Task<IReadOnlyList<CycleLoad>> ListCycleLoadsAsync(Guid cycleId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<CycleLoad>>(data.CycleLoads.Where(x => x.CycleId == cycleId).ToList());
    public Task InsertCycleIndicatorAsync(CycleIndicator indicator, CancellationToken ct) { data.CycleIndicators.Add(indicator); return Task.CompletedTask; }
    public Task<IReadOnlyList<CycleIndicator>> ListCycleIndicatorsAsync(Guid cycleId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<CycleIndicator>>(data.CycleIndicators.Where(x => x.CycleId == cycleId).ToList());
    public Task<CssdIssue?> FindCssdIssueAsync(Guid id, CancellationToken ct) => Task.FromResult(data.CssdIssues.FirstOrDefault(x => x.Id == id));
    public Task InsertCssdIssueAsync(CssdIssue issue, CancellationToken ct) { data.CssdIssues.Add(issue); return Task.CompletedTask; }
    public Task UpdateCssdIssueAsync(CssdIssue issue, CancellationToken ct) { var old = data.CssdIssues.FirstOrDefault(x => x.Id == issue.Id) ?? throw new DomainException("not_found", "CSSD issue not found.", 404); CareVersion(old.Version, issue.Version); data.CssdIssues[data.CssdIssues.IndexOf(old)] = issue; return Task.CompletedTask; }
    public Task<IReadOnlyList<CssdIssue>> ListCssdIssuesBySetAsync(Guid setId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<CssdIssue>>(data.CssdIssues.Where(x => x.SetId == setId).ToList());
    public Task<CssdRecall?> FindCssdRecallAsync(Guid id, CancellationToken ct) => Task.FromResult(data.CssdRecalls.FirstOrDefault(x => x.Id == id));
    public Task<CssdRecall?> FindOpenRecallByCycleAsync(Guid cycleId, CancellationToken ct) =>
        Task.FromResult(data.CssdRecalls.FirstOrDefault(x => x.CycleId == cycleId && x.Status == "Open"));
    public Task InsertCssdRecallAsync(CssdRecall recall, CancellationToken ct) { data.CssdRecalls.Add(recall); return Task.CompletedTask; }
    public Task UpdateCssdRecallAsync(CssdRecall recall, CancellationToken ct) { var old = data.CssdRecalls.FirstOrDefault(x => x.Id == recall.Id) ?? throw new DomainException("not_found", "Recall not found.", 404); data.CssdRecalls[data.CssdRecalls.IndexOf(old)] = recall; return Task.CompletedTask; }
    public Task<IReadOnlyList<CssdRecall>> ListCssdRecallsAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<CssdRecall>>(data.CssdRecalls.ToList());
}
