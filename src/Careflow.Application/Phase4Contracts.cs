using Careflow.Domain;
namespace Careflow.Application;

// Phase 4 inputs (insurance/TPA, incentives, telemedicine, inventory/procurement, CSSD).
public sealed record CoverageInput(Guid PatientId, Guid PlanId, string PolicyNumber, string MemberId,
    DateOnly ValidFrom, DateOnly ValidTo, decimal SumInsured, decimal Deductible);
public sealed record PreauthInput(Guid CoverageId, Guid PatientId, string DiagnosisCode,
    string ProcedureCode, decimal RequestedAmount, DateOnly ValidTo);
public sealed record PreauthRespondInput(string Decision, decimal? ApprovedAmount, string Note, long Version);
public sealed record ClaimLineInput(string ServiceCode, string Description, decimal Quantity, decimal UnitPrice);
public sealed record ClaimInput(Guid PatientId, Guid CoverageId, Guid? InvoiceId, Guid? AdmissionId,
    string DiagnosisCode, IReadOnlyList<ClaimLineInput> Lines);
public sealed record ClaimQueryInput(string Question);
public sealed record ClaimRespondInput(string Response, long Version);
public sealed record ClaimDecisionInput(string Decision, decimal? ApprovedAmount, string Reason, long Version);
public sealed record SettlementInput(decimal Amount, string Mode, string Reference, long Version);

public sealed record IncentiveRuleInput(string Name, string ScopeKind, Guid? DoctorId, string Basis,
    string Mode, decimal Value, DateOnly EffectiveFrom, DateOnly? EffectiveTo);
public sealed record IncentiveBatchInput(DateOnly PeriodStart, DateOnly PeriodEnd);
public sealed record BatchApproveInput(string Reason, long Version);
public sealed record BatchPayInput(decimal Amount, string Mode, string Reference, long Version);
public sealed record BatchRecalcInput(string Reason, long Version);

public sealed record TeleSessionInput(Guid AppointmentId);
public sealed record TeleConsentInput(string Kind, bool Given, string Note);
public sealed record TeleEndInput(string Note, string EmergencyAdvice, long Version);
public sealed record TeleUploadInput(string FileName, string ContentType, long SizeBytes);

public sealed record IndentInput(Guid StoreId, Guid ItemId, int Quantity, string Priority, string Reason);
public sealed record IndentDecisionInput(bool Approve, string Note, long Version);
public sealed record PurchaseOrderInput(string VendorName, Guid IndentId, int Quantity, decimal UnitCost);
public sealed record GoodsReceiptInput(int ReceivedQty, int AcceptedQty, int RejectedQty,
    string InspectionNote, string BatchNumber, DateOnly? ExpiryDate, long Version);
public sealed record StockTransferInput(Guid FromStoreId, Guid ToStoreId, Guid ItemId, Guid BatchId,
    int Quantity, string Note);
public sealed record TransferReceiveInput(long Version);
public sealed record ConsumptionInput(Guid StoreId, Guid ItemId, Guid BatchId, int Quantity,
    string Department, string Reason);

public sealed record CssdItemInput(string Name, string SerialNumber);
public sealed record CssdSetInput(string Code, string Name, IReadOnlyList<CssdItemInput> Items);
public sealed record CssdReceiveInput(string ReturnCondition, string Note);
public sealed record CycleInput(string Method, string Machine, decimal TempC, int DurationMin, IReadOnlyList<Guid> SetIds);
public sealed record IndicatorInput(string Kind, string Result, string Note);
public sealed record CycleReleaseInput(long Version, string Reason = "");
public sealed record CssdIssueInput(Guid SetId, Guid? BookingId, string IssuedTo);
public sealed record CssdReturnInput(string ReturnCondition, long Version);
public sealed record RecallInput(Guid CycleId, string Reason);
public sealed record TeleJoin(Guid SessionId, string Token, DateTimeOffset ExpiresAt);

public partial interface IHospitalStore
{
    // Insurance / TPA
    Task<IReadOnlyList<Payer>> ListPayersAsync(CancellationToken ct);
    Task<InsurancePlan?> FindInsurancePlanAsync(Guid id, CancellationToken ct);
    Task<Coverage?> FindCoverageAsync(Guid id, CancellationToken ct);
    Task<Coverage?> FindCoverageByPolicyAsync(Guid patientId, string policyNumber, CancellationToken ct);
    Task InsertCoverageAsync(Coverage coverage, CancellationToken ct);
    Task UpdateCoverageAsync(Coverage coverage, CancellationToken ct);
    Task<IReadOnlyList<Coverage>> ListCoveragesAsync(Guid? patientId, CancellationToken ct);
    Task<Preauthorization?> FindPreauthAsync(Guid id, CancellationToken ct);
    Task InsertPreauthAsync(Preauthorization preauth, CancellationToken ct);
    Task UpdatePreauthAsync(Preauthorization preauth, CancellationToken ct);
    Task<IReadOnlyList<Preauthorization>> ListPreauthsAsync(Guid? patientId, CancellationToken ct);
    Task<Claim?> FindClaimAsync(Guid id, CancellationToken ct);
    Task<Claim?> FindClaimByInvoiceAsync(Guid invoiceId, CancellationToken ct);
    Task InsertClaimAsync(Claim claim, CancellationToken ct);
    Task UpdateClaimAsync(Claim claim, CancellationToken ct);
    Task<IReadOnlyList<Claim>> ListClaimsAsync(Guid? patientId, CancellationToken ct);
    Task InsertClaimLineAsync(ClaimLine line, CancellationToken ct);
    Task<IReadOnlyList<ClaimLine>> ListClaimLinesAsync(Guid claimId, CancellationToken ct);
    Task InsertClaimQueryAsync(ClaimQuery query, CancellationToken ct);
    Task<IReadOnlyList<ClaimQuery>> ListClaimQueriesAsync(Guid claimId, CancellationToken ct);
    Task UpdateClaimQueryAsync(ClaimQuery query, CancellationToken ct);
    Task<Settlement?> FindSettlementByReferenceAsync(string reference, CancellationToken ct);
    Task InsertSettlementAsync(Settlement settlement, CancellationToken ct);
    Task<IReadOnlyList<Settlement>> ListSettlementsAsync(Guid claimId, CancellationToken ct);

    // Incentives
    Task<IncentiveRule?> FindIncentiveRuleAsync(Guid id, CancellationToken ct);
    Task InsertIncentiveRuleAsync(IncentiveRule rule, CancellationToken ct);
    Task UpdateIncentiveRuleAsync(IncentiveRule rule, CancellationToken ct);
    Task<IReadOnlyList<IncentiveRule>> ListIncentiveRulesAsync(CancellationToken ct);
    Task<bool> HasOverlappingIncentiveRuleAsync(string scopeKind, Guid? doctorId, string basis, DateOnly from, DateOnly? to, Guid? excludeId, CancellationToken ct);
    Task<IReadOnlyList<IncentiveRule>> ListActiveIncentiveRulesAsync(DateOnly on, CancellationToken ct);
    Task<IncentiveBatch?> FindIncentiveBatchAsync(Guid id, CancellationToken ct);
    Task<IncentiveBatch?> FindIncentiveBatchByPeriodAsync(DateOnly from, DateOnly to, CancellationToken ct);
    Task InsertIncentiveBatchAsync(IncentiveBatch batch, CancellationToken ct);
    Task UpdateIncentiveBatchAsync(IncentiveBatch batch, CancellationToken ct);
    Task<IReadOnlyList<IncentiveBatch>> ListIncentiveBatchesAsync(CancellationToken ct);
    Task InsertIncentiveLineAsync(IncentiveLine line, CancellationToken ct);
    Task<IReadOnlyList<IncentiveLine>> ListIncentiveLinesAsync(Guid batchId, CancellationToken ct);
    Task DeleteIncentiveLinesAsync(Guid batchId, CancellationToken ct);
    Task<IReadOnlyList<IncentivePayment>> ListIncentivePaymentsAsync(Guid batchId, CancellationToken ct);
    Task InsertIncentivePaymentAsync(IncentivePayment payment, CancellationToken ct);
    Task<IReadOnlyList<IncentiveEvent>> ListIncentiveEventsAsync(DateOnly from, DateOnly to, CancellationToken ct);

    // Telemedicine
    Task<TeleSession?> FindTeleSessionAsync(Guid id, CancellationToken ct);
    Task<TeleSession?> FindTeleSessionByAppointmentAsync(Guid appointmentId, CancellationToken ct);
    Task InsertTeleSessionAsync(TeleSession session, CancellationToken ct);
    Task UpdateTeleSessionAsync(TeleSession session, CancellationToken ct);
    Task<IReadOnlyList<TeleSession>> ListTeleSessionsAsync(Guid? patientId, CancellationToken ct);
    Task InsertTeleWaitingEventAsync(TeleWaitingEvent evt, CancellationToken ct);
    Task<IReadOnlyList<TeleWaitingEvent>> ListTeleWaitingEventsAsync(Guid sessionId, CancellationToken ct);
    Task InsertTeleConsentEventAsync(TeleConsentEvent evt, CancellationToken ct);
    Task<IReadOnlyList<TeleConsentEvent>> ListTeleConsentEventsAsync(Guid sessionId, CancellationToken ct);
    Task InsertTeleUploadAsync(TeleUpload upload, CancellationToken ct);
    Task<IReadOnlyList<TeleUpload>> ListTeleUploadsAsync(Guid sessionId, CancellationToken ct);

    // Inventory and procurement
    Task<InventoryItem?> FindInventoryItemAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<InventoryItem>> ListInventoryItemsAsync(CancellationToken ct);
    Task<InventoryStore?> FindInventoryStoreAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<InventoryStore>> ListInventoryStoresAsync(CancellationToken ct);
    Task<Vendor?> FindVendorByNameAsync(string name, CancellationToken ct);
    Task<IReadOnlyList<Vendor>> ListVendorsAsync(CancellationToken ct);
    Task InsertVendorAsync(Vendor vendor, CancellationToken ct);
    Task<InventoryBatch?> FindInventoryBatchAsync(Guid id, CancellationToken ct);
    Task<InventoryBatch?> FindInventoryBatchByNumberAsync(Guid itemId, string batchNumber, Guid storeId, CancellationToken ct);
    Task InsertInventoryBatchAsync(InventoryBatch batch, CancellationToken ct);
    Task UpdateInventoryBatchAsync(InventoryBatch batch, CancellationToken ct);
    Task<IReadOnlyList<InventoryBatch>> ListInventoryBatchesAsync(Guid? storeId, Guid? itemId, CancellationToken ct);
    Task InsertInventoryMovementAsync(InventoryMovement movement, CancellationToken ct);
    Task<IReadOnlyList<InventoryMovement>> ListInventoryMovementsAsync(Guid? batchId, CancellationToken ct);
    Task<InventoryIndent?> FindInventoryIndentAsync(Guid id, CancellationToken ct);
    Task InsertInventoryIndentAsync(InventoryIndent indent, CancellationToken ct);
    Task UpdateInventoryIndentAsync(InventoryIndent indent, CancellationToken ct);
    Task<IReadOnlyList<InventoryIndent>> ListInventoryIndentsAsync(CancellationToken ct);
    Task<PurchaseOrder?> FindPurchaseOrderAsync(Guid id, CancellationToken ct);
    Task<PurchaseOrder?> FindPurchaseOrderByIndentAsync(Guid indentId, CancellationToken ct);
    Task InsertPurchaseOrderAsync(PurchaseOrder order, CancellationToken ct);
    Task UpdatePurchaseOrderAsync(PurchaseOrder order, CancellationToken ct);
    Task<IReadOnlyList<PurchaseOrder>> ListPurchaseOrdersAsync(CancellationToken ct);
    Task<GoodsReceipt?> FindGoodsReceiptByPoAsync(Guid purchaseOrderId, CancellationToken ct);
    Task InsertGoodsReceiptAsync(GoodsReceipt receipt, CancellationToken ct);
    Task<InventoryTransfer?> FindTransferAsync(Guid id, CancellationToken ct);
    Task InsertTransferAsync(InventoryTransfer transfer, CancellationToken ct);
    Task UpdateTransferAsync(InventoryTransfer transfer, CancellationToken ct);
    Task<IReadOnlyList<InventoryTransfer>> ListTransfersAsync(CancellationToken ct);
    Task InsertConsumptionAsync(InventoryConsumption consumption, CancellationToken ct);
    Task<IReadOnlyList<InventoryConsumption>> ListConsumptionsAsync(CancellationToken ct);
    Task<Asset?> FindAssetBySerialAsync(string serialNumber, CancellationToken ct);
    Task InsertAssetAsync(Asset asset, CancellationToken ct);
    Task<IReadOnlyList<Asset>> ListAssetsAsync(Guid? itemId, CancellationToken ct);

    // CSSD
    Task<InstrumentSet?> FindInstrumentSetAsync(Guid id, CancellationToken ct);
    Task InsertInstrumentSetAsync(InstrumentSet set, CancellationToken ct);
    Task UpdateInstrumentSetAsync(InstrumentSet set, CancellationToken ct);
    Task<IReadOnlyList<InstrumentSet>> ListInstrumentSetsAsync(CancellationToken ct);
    Task InsertInstrumentItemAsync(InstrumentItem item, CancellationToken ct);
    Task<IReadOnlyList<InstrumentItem>> ListInstrumentItemsAsync(Guid setId, CancellationToken ct);
    Task<SterilizerCycle?> FindCycleAsync(Guid id, CancellationToken ct);
    Task InsertCycleAsync(SterilizerCycle cycle, CancellationToken ct);
    Task UpdateCycleAsync(SterilizerCycle cycle, CancellationToken ct);
    Task<IReadOnlyList<SterilizerCycle>> ListCyclesAsync(CancellationToken ct);
    Task InsertCycleLoadAsync(CycleLoad load, CancellationToken ct);
    Task<IReadOnlyList<CycleLoad>> ListCycleLoadsAsync(Guid cycleId, CancellationToken ct);
    Task InsertCycleIndicatorAsync(CycleIndicator indicator, CancellationToken ct);
    Task<IReadOnlyList<CycleIndicator>> ListCycleIndicatorsAsync(Guid cycleId, CancellationToken ct);
    Task<CssdIssue?> FindCssdIssueAsync(Guid id, CancellationToken ct);
    Task InsertCssdIssueAsync(CssdIssue issue, CancellationToken ct);
    Task UpdateCssdIssueAsync(CssdIssue issue, CancellationToken ct);
    Task<IReadOnlyList<CssdIssue>> ListCssdIssuesBySetAsync(Guid setId, CancellationToken ct);
    Task<CssdRecall?> FindCssdRecallAsync(Guid id, CancellationToken ct);
    Task<CssdRecall?> FindOpenRecallByCycleAsync(Guid cycleId, CancellationToken ct);
    Task InsertCssdRecallAsync(CssdRecall recall, CancellationToken ct);
    Task UpdateCssdRecallAsync(CssdRecall recall, CancellationToken ct);
    Task<IReadOnlyList<CssdRecall>> ListCssdRecallsAsync(CancellationToken ct);
}
