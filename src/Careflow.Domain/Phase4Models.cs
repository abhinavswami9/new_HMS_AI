namespace Careflow.Domain;

// Phase 4: insurance/TPA, doctor incentives, telemedicine, inventory/procurement and CSSD.
// Integration boundaries kept explicit: no live payer gateway, video provider, payment
// service or procurement marketplace is connected anywhere in this slice.

// ── Insurance / TPA ─────────────────────────────────────────────────────────────
public sealed record Payer(Guid Id, string Code, string Name, string Contact, bool Active);
public sealed record InsurancePlan(Guid Id, Guid PayerId, string Code, string Name,
    string PolicyType, decimal SumInsuredLimit);
public sealed record Coverage(Guid Id, string Number, Guid PatientId, Guid PlanId,
    string PolicyNumber, string MemberId, DateOnly ValidFrom, DateOnly ValidTo,
    decimal SumInsured, decimal Deductible, string Status, long Version = 1);
public sealed record Preauthorization(Guid Id, string Number, Guid CoverageId, Guid PatientId,
    string DiagnosisCode, string ProcedureCode, decimal RequestedAmount, string Status,
    DateOnly? ValidFrom, DateOnly? ValidTo, decimal? ApprovedAmount, string ResponseNote,
    Guid? RespondedBy, DateTimeOffset? RespondedAt, Guid RequestedBy, DateTimeOffset RequestedAt, long Version = 1);
public sealed record Claim(Guid Id, string Number, Guid PatientId, Guid CoverageId,
    Guid? InvoiceId, Guid? AdmissionId, string DiagnosisCode, decimal TotalAmount,
    decimal ApprovedAmount, decimal PatientLiability, string Status, string QueryNote,
    string ResponseNote, string RejectionReason, Guid? SubmittedBy, DateTimeOffset? SubmittedAt,
    Guid CreatedBy, DateTimeOffset CreatedAt, long Version = 1);
public sealed record ClaimLine(Guid Id, Guid ClaimId, string ServiceCode, string Description,
    decimal Quantity, decimal UnitPrice, decimal Amount, decimal ApprovedAmount);
public sealed record ClaimQuery(Guid Id, Guid ClaimId, string Question, string? Response,
    Guid RaisedBy, DateTimeOffset RaisedAt, Guid? RespondedBy, DateTimeOffset? RespondedAt);
public sealed record Settlement(Guid Id, string Number, Guid ClaimId, decimal Amount,
    string Mode, string Reference, DateTimeOffset SettledAt, Guid SettledBy);

// ── Doctor incentives ───────────────────────────────────────────────────────────
public sealed record IncentiveRule(Guid Id, string Name, string ScopeKind, Guid? DoctorId,
    string Basis, string Mode, decimal Value, DateOnly EffectiveFrom, DateOnly? EffectiveTo,
    string Status, Guid CreatedBy, long Version = 1);
public sealed record IncentiveBatch(Guid Id, string Number, DateOnly PeriodStart, DateOnly PeriodEnd,
    string Status, decimal TotalAmount, string CalculatedBy, DateTimeOffset CalculatedAt,
    string ApprovedBy, DateTimeOffset? ApprovedAt, string? RecalculationReason, long Version = 1);
public sealed record IncentiveLine(Guid Id, Guid BatchId, Guid DoctorId, Guid RuleId,
    string BasisLabel, decimal BasisAmount, decimal IncentiveAmount, string Snapshot);
public sealed record IncentivePayment(Guid Id, Guid BatchId, decimal Amount, string Mode,
    string Reference, Guid PaidBy, DateTimeOffset PaidAt);
public sealed record IncentiveEvent(string Basis, Guid DoctorId, string Ref, decimal Amount, DateTimeOffset At);

// ── Telemedicine ────────────────────────────────────────────────────────────────
public sealed record TeleSession(Guid Id, string Number, Guid AppointmentId, Guid PatientId,
    Guid DoctorId, string Status, DateTimeOffset ScheduledAt, bool ConsentObtained,
    DateTimeOffset? StartedAt, DateTimeOffset? EndedAt, string EndNote, string EmergencyAdvice,
    long Version = 1);
public sealed record TeleWaitingEvent(Guid Id, Guid SessionId, string Kind, Guid ActorId, DateTimeOffset At);
public sealed record TeleConsentEvent(Guid Id, Guid SessionId, string Kind, bool Given,
    string Note, Guid ActorId, DateTimeOffset At);
public sealed record TeleUpload(Guid Id, Guid SessionId, string FileName, string ContentType,
    long SizeBytes, Guid UploadedBy, DateTimeOffset UploadedAt);

// ── Inventory and procurement ───────────────────────────────────────────────────
public sealed record InventoryItem(Guid Id, string Code, string Name, string Category,
    string Uom, int ReorderLevel, decimal UnitCost, bool Active);
public sealed record InventoryStore(Guid Id, string Code, string Name, string Kind);
public sealed record Vendor(Guid Id, string Code, string Name, bool Authorized);
public sealed record InventoryBatch(Guid Id, string BatchNumber, Guid ItemId, Guid StoreId,
    DateOnly? ExpiryDate, int Quantity, decimal UnitCost, long Version = 1);
public sealed record InventoryMovement(Guid Id, Guid ItemId, Guid StoreId, Guid? BatchId,
    string Kind, int Quantity, string RefType, string RefId, Guid By, DateTimeOffset At);
public sealed record InventoryIndent(Guid Id, string Number, Guid StoreId, Guid ItemId,
    int Quantity, string Priority, string Status, string Reason, Guid RaisedBy, DateTimeOffset RaisedAt,
    Guid? ApprovedBy, DateTimeOffset? ApprovedAt, long Version = 1);
public sealed record PurchaseOrder(Guid Id, string Number, Guid VendorId, Guid ItemId,
    Guid IndentId, int Quantity, decimal UnitCost, string Status, Guid RaisedBy, DateTimeOffset RaisedAt,
    Guid? ReceivedBy, DateTimeOffset? ReceivedAt, long Version = 1);
public sealed record GoodsReceipt(Guid Id, string Number, Guid PurchaseOrderId, int ReceivedQty,
    int AcceptedQty, int RejectedQty, string InspectionNote, Guid ReceivedBy, DateTimeOffset ReceivedAt);
public sealed record InventoryTransfer(Guid Id, string Number, Guid FromStoreId, Guid ToStoreId,
    Guid ItemId, Guid BatchId, int Quantity, string Status, string Note,
    Guid DispatchedBy, DateTimeOffset DispatchedAt, Guid? ReceivedBy, DateTimeOffset? ReceivedAt, long Version = 1);
public sealed record InventoryConsumption(Guid Id, string Number, Guid StoreId, Guid ItemId,
    Guid BatchId, int Quantity, string Department, string Reason, Guid By, DateTimeOffset At);
public sealed record Asset(Guid Id, string Tag, Guid ItemId, string SerialNumber, string Status,
    string Custodian, Guid By, DateTimeOffset At);

// ── CSSD ────────────────────────────────────────────────────────────────────────
public sealed record InstrumentSet(Guid Id, string Code, string Name, string Status,
    int MissingCount, DateTimeOffset? SterileExpiry, Guid? LastCycleId, long Version = 1);
public sealed record InstrumentItem(Guid Id, Guid SetId, string Name, string SerialNumber,
    string Condition);
public sealed record SterilizerCycle(Guid Id, string Number, string Method, string Machine,
    decimal TempC, int DurationMin, string Status, DateTimeOffset StartedAt, DateTimeOffset? EndedAt,
    Guid Operator, long Version = 1);
public sealed record CycleLoad(Guid Id, Guid CycleId, Guid SetId, string SetStatus);
public sealed record CycleIndicator(Guid Id, Guid CycleId, string Kind, string Result,
    string Note, Guid By, DateTimeOffset At);
public sealed record CssdIssue(Guid Id, string Number, Guid SetId, Guid? BookingId, string IssuedTo,
    string Status, string ReturnCondition, DateTimeOffset IssuedAt, Guid IssuedBy,
    DateTimeOffset? ReturnedAt, long Version = 1);
public sealed record CssdRecall(Guid Id, string Number, Guid CycleId, string Reason, string Status,
    Guid By, DateTimeOffset At);

public static class Phase4Rules
{
    public static readonly IReadOnlyList<string> ClaimStatuses = ["Draft", "Submitted", "Queried", "Responded", "Approved", "Rejected", "Settled"];
    public static readonly IReadOnlyList<string> PreauthStatuses = ["Requested", "Approved", "Rejected", "Expired"];
    public static readonly IReadOnlyList<string> SettlementModes = ["Bank transfer", "Cheque", "Cash", "Adjustment"];
    public static readonly IReadOnlyList<string> IncentiveBases = ["Revenue", "Consultation", "Procedure", "Surgery"];
    public static readonly IReadOnlyList<string> IncentiveModes = ["Fixed", "Percent"];
    public static readonly IReadOnlyList<string> IncentiveScopes = ["Doctor", "All"];
    public static readonly IReadOnlyList<string> ItemCategories = ["Medical", "Surgical", "Consumable", "Asset"];
    public static readonly IReadOnlyList<string> IndentPriorities = ["Routine", "Urgent", "Emergency"];
    public static readonly IReadOnlyList<string> CssdSetStatuses = ["Dirty", "Cleaning", "Packed", "Sterilizing", "Sterile", "Issued", "Quarantined"];
    public static readonly IReadOnlyList<string> CssdCycleStatuses = ["Running", "Passed", "Failed"];
    public static readonly IReadOnlyList<string> CssdMethods = ["Steam", "Ethylene oxide", "Plasma"];
    public static readonly IReadOnlyList<string> ReturnConditions = ["Complete", "Incomplete", "Damaged"];

    public static readonly IReadOnlyList<string> UploadExtensions = [".pdf", ".png", ".jpg", ".jpeg", ".docx"];
    public const long UploadMaxBytes = 10 * 1024 * 1024;

    public static bool CoverageCovers(Coverage c, DateOnly on) =>
        c.Status == "Active" && on >= c.ValidFrom && on <= c.ValidTo;

    public static decimal ClampPercent(decimal value) => value is < 0 or > 100 ? -1 : value;
}
