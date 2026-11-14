using Careflow.Application;
using Careflow.Domain;
namespace Careflow.Api;

public static class Phase4Endpoints
{
    public static void Map(RouteGroupBuilder root)
    {
        Careflow.Application.Scope S(HttpContext c) => Careflow.Application.Scope.For((Principal)c.Items["principal"]!, c.RequestServices.GetRequiredService<IClock>().UtcNow);

        var insurance = root.MapGroup("/insurance");
        insurance.MapGet("/coverages", async (Guid? patientId, HttpContext c, InsuranceService s, CancellationToken ct) => Results.Ok(await s.ListCoverages(S(c), patientId, ct)));
        insurance.MapPost("/coverages", async (CoverageInput input, HttpContext c, InsuranceService s, CancellationToken ct) => Results.Ok(await s.RegisterCoverage(S(c), input, ct)));
        insurance.MapPost("/coverages/{id:guid}/verify", async (Guid id, HttpContext c, InsuranceService s, CancellationToken ct) => Results.Ok(await s.VerifyCoverage(S(c), id, ct)));
        insurance.MapGet("/preauthorizations", async (Guid? patientId, HttpContext c, InsuranceService s, CancellationToken ct) => Results.Ok(await s.ListPreauths(S(c), patientId, ct)));
        insurance.MapPost("/preauthorizations", async (PreauthInput input, HttpContext c, InsuranceService s, CancellationToken ct) => Results.Ok(await s.RequestPreauth(S(c), input, ct)));
        insurance.MapPost("/preauthorizations/{id:guid}/respond", async (Guid id, PreauthRespondInput input, HttpContext c, InsuranceService s, CancellationToken ct) => Results.Ok(await s.RespondPreauth(S(c), id, input, ct)));
        insurance.MapGet("/claims", async (Guid? patientId, HttpContext c, InsuranceService s, CancellationToken ct) => Results.Ok(await s.ListClaims(S(c), patientId, ct)));
        insurance.MapPost("/claims", async (ClaimInput input, HttpContext c, InsuranceService s, CancellationToken ct) => Results.Ok(await s.CreateClaim(S(c), input, ct)));
        insurance.MapPost("/claims/{id:guid}/submit", async (Guid id, VersionInput input, HttpContext c, InsuranceService s, CancellationToken ct) => Results.Ok(await s.SubmitClaim(S(c), id, input, ct)));
        insurance.MapPost("/claims/{id:guid}/query", async (Guid id, ClaimQueryInput input, HttpContext c, InsuranceService s, CancellationToken ct) => Results.Ok(await s.RaiseQuery(S(c), id, input, ct)));
        insurance.MapPost("/claims/{id:guid}/respond", async (Guid id, ClaimRespondInput input, HttpContext c, InsuranceService s, CancellationToken ct) => Results.Ok(await s.RespondQuery(S(c), id, input, ct)));
        insurance.MapPost("/claims/{id:guid}/decide", async (Guid id, ClaimDecisionInput input, HttpContext c, InsuranceService s, CancellationToken ct) => Results.Ok(await s.DecideClaim(S(c), id, input, ct)));
        insurance.MapPost("/claims/{id:guid}/settle", async (Guid id, SettlementInput input, HttpContext c, InsuranceService s, CancellationToken ct) => Results.Ok(await s.SettleClaim(S(c), id, input, ct)));

        var incentives = root.MapGroup("/incentives");
        incentives.MapGet("/rules", async (HttpContext c, IncentiveService s, CancellationToken ct) => Results.Ok(await s.ListRules(S(c), ct)));
        incentives.MapPost("/rules", async (IncentiveRuleInput input, HttpContext c, IncentiveService s, CancellationToken ct) => Results.Ok(await s.CreateRule(S(c), input, ct)));
        incentives.MapPost("/rules/{id:guid}/status/{status}", async (Guid id, string status, VersionInput input, HttpContext c, IncentiveService s, CancellationToken ct) => Results.Ok(await s.SetRuleStatus(S(c), id, status, input, ct)));
        incentives.MapGet("/batches", async (HttpContext c, IncentiveService s, CancellationToken ct) => Results.Ok(await s.ListBatches(S(c), ct)));
        incentives.MapGet("/batches/{id:guid}/lines", async (Guid id, HttpContext c, IncentiveService s, CancellationToken ct) => Results.Ok(await s.ListLines(S(c), id, ct)));
        incentives.MapPost("/calculate", async (IncentiveBatchInput input, HttpContext c, IncentiveService s, CancellationToken ct) => Results.Ok(await s.Calculate(S(c), input, ct)));
        incentives.MapPost("/batches/{id:guid}/recalculate", async (Guid id, BatchRecalcInput input, HttpContext c, IncentiveService s, CancellationToken ct) => Results.Ok(await s.Recalculate(S(c), id, input, ct)));
        incentives.MapPost("/batches/{id:guid}/approve", async (Guid id, BatchApproveInput input, HttpContext c, IncentiveService s, CancellationToken ct) => Results.Ok(await s.Approve(S(c), id, input, ct)));
        incentives.MapPost("/batches/{id:guid}/pay", async (Guid id, BatchPayInput input, HttpContext c, IncentiveService s, CancellationToken ct) => Results.Ok(await s.Pay(S(c), id, input, ct)));

        var tele = root.MapGroup("/telemedicine");
        tele.MapGet("/sessions", async (Guid? patientId, HttpContext c, TelemedicineService s, CancellationToken ct) => Results.Ok(await s.ListSessions(S(c), patientId, ct)));
        tele.MapPost("/sessions", async (TeleSessionInput input, HttpContext c, TelemedicineService s, CancellationToken ct) => Results.Ok(await s.Book(S(c), input, ct)));
        tele.MapPost("/sessions/{id:guid}/consent", async (Guid id, TeleConsentInput input, HttpContext c, TelemedicineService s, CancellationToken ct) => Results.Ok(await s.RecordConsent(S(c), id, input, ct)));
        tele.MapPost("/sessions/{id:guid}/join-waiting", async (Guid id, HttpContext c, TelemedicineService s, CancellationToken ct) => Results.Ok(await s.JoinWaiting(S(c), id, ct)));
        // Ephemeral application-side join credential (docs/PHASE4_STATUS.md): no live video
        // provider is connected. The token authorizes the waiting-room workflow only.
        tele.MapPost("/sessions/{id:guid}/join-token", async (Guid id, VersionInput input, HttpContext c, TelemedicineService s, CancellationToken ct) => Results.Ok(await s.IssueJoinToken(S(c), id, input, ct)));
        tele.MapPost("/sessions/{id:guid}/admit", async (Guid id, VersionInput input, HttpContext c, TelemedicineService s, CancellationToken ct) => Results.Ok(await s.Admit(S(c), id, input, ct)));
        tele.MapPost("/sessions/{id:guid}/end", async (Guid id, TeleEndInput input, HttpContext c, TelemedicineService s, CancellationToken ct) => Results.Ok(await s.End(S(c), id, input, ct)));
        tele.MapPost("/sessions/{id:guid}/uploads", async (Guid id, TeleUploadInput input, HttpContext c, TelemedicineService s, CancellationToken ct) => Results.Ok(await s.Upload(S(c), id, input, ct)));
        tele.MapPost("/sessions/{id:guid}/cancel", async (Guid id, VersionInput input, HttpContext c, TelemedicineService s, CancellationToken ct) => Results.Ok(await s.Cancel(S(c), id, input, ct)));

        var inventory = root.MapGroup("/inventory");
        inventory.MapGet("/stores", async (HttpContext c, InventoryService s, CancellationToken ct) => Results.Ok(await s.ListStores(S(c), ct)));
        inventory.MapGet("/items", async (HttpContext c, InventoryService s, CancellationToken ct) => Results.Ok(await s.ListItems(S(c), ct)));
        inventory.MapGet("/vendors", async (HttpContext c, InventoryService s, CancellationToken ct) => Results.Ok(await s.ListVendors(S(c), ct)));
        inventory.MapGet("/batches", async (Guid? storeId, Guid? itemId, HttpContext c, InventoryService s, CancellationToken ct) => Results.Ok(await s.ListBatches(S(c), storeId, itemId, ct)));
        inventory.MapGet("/indents", async (HttpContext c, InventoryService s, CancellationToken ct) => Results.Ok(await s.ListIndents(S(c), ct)));
        inventory.MapPost("/indents", async (IndentInput input, HttpContext c, InventoryService s, CancellationToken ct) => Results.Ok(await s.RaiseIndent(S(c), input, ct)));
        inventory.MapPost("/indents/{id:guid}/decision", async (Guid id, IndentDecisionInput input, HttpContext c, InventoryService s, CancellationToken ct) => Results.Ok(await s.DecideIndent(S(c), id, input, ct)));
        inventory.MapGet("/purchase-orders", async (HttpContext c, InventoryService s, CancellationToken ct) => Results.Ok(await s.ListPurchaseOrders(S(c), ct)));
        inventory.MapPost("/purchase-orders", async (PurchaseOrderInput input, HttpContext c, InventoryService s, CancellationToken ct) => Results.Ok(await s.CreatePO(S(c), input, ct)));
        inventory.MapPost("/purchase-orders/{id:guid}/goods-receipt", async (Guid id, GoodsReceiptInput input, HttpContext c, InventoryService s, CancellationToken ct) => Results.Ok(await s.ReceiveGoods(S(c), id, input, ct)));
        inventory.MapPost("/transfers", async (StockTransferInput input, HttpContext c, InventoryService s, CancellationToken ct) => Results.Ok(await s.DispatchTransfer(S(c), input, ct)));
        inventory.MapPost("/transfers/{id:guid}/receive", async (Guid id, TransferReceiveInput input, HttpContext c, InventoryService s, CancellationToken ct) => Results.Ok(await s.ReceiveTransfer(S(c), id, input, ct)));
        inventory.MapPost("/consumptions", async (ConsumptionInput input, HttpContext c, InventoryService s, CancellationToken ct) => Results.Ok(await s.Consume(S(c), input, ct)));

        var cssd = root.MapGroup("/cssd");
        cssd.MapGet("/sets", async (HttpContext c, CssdService s, CancellationToken ct) => Results.Ok(await s.ListSets(S(c), ct)));
        cssd.MapPost("/sets", async (CssdSetInput input, HttpContext c, CssdService s, CancellationToken ct) => Results.Ok(await s.RegisterSet(S(c), input, ct)));
        cssd.MapPost("/sets/{id:guid}/return", async (Guid id, CssdReceiveInput input, HttpContext c, CssdService s, CancellationToken ct) => Results.Ok(await s.ReceiveReturn(S(c), id, input, ct)));
        cssd.MapPost("/sets/{id:guid}/clean-pack", async (Guid id, VersionInput input, HttpContext c, CssdService s, CancellationToken ct) => Results.Ok(await s.CleanPack(S(c), id, input, ct)));
        cssd.MapGet("/cycles", async (HttpContext c, CssdService s, CancellationToken ct) => Results.Ok(await s.ListCycles(S(c), ct)));
        cssd.MapPost("/cycles", async (CycleInput input, HttpContext c, CssdService s, CancellationToken ct) => Results.Ok(await s.StartCycle(S(c), input, ct)));
        cssd.MapPost("/cycles/{id:guid}/indicators", async (Guid id, IndicatorInput input, HttpContext c, CssdService s, CancellationToken ct) => Results.Ok(await s.RecordIndicator(S(c), id, input, ct)));
        cssd.MapPost("/cycles/{id:guid}/release", async (Guid id, CycleReleaseInput input, HttpContext c, CssdService s, CancellationToken ct) => Results.Ok(await s.ReleaseCycle(S(c), id, input, ct)));
        cssd.MapPost("/issues", async (CssdIssueInput input, HttpContext c, CssdService s, CancellationToken ct) => Results.Ok(await s.IssueSet(S(c), input, ct)));
        cssd.MapPost("/issues/{id:guid}/return", async (Guid id, CssdReturnInput input, HttpContext c, CssdService s, CancellationToken ct) => Results.Ok(await s.ReturnIssue(S(c), id, input, ct)));
        cssd.MapPost("/recalls", async (RecallInput input, HttpContext c, CssdService s, CancellationToken ct) => Results.Ok(await s.Recall(S(c), input, ct)));
        cssd.MapPost("/recalls/{id:guid}/close", async (Guid id, VersionInput input, HttpContext c, CssdService s, CancellationToken ct) => Results.Ok(await s.CloseRecall(S(c), id, input, ct)));
    }
}
