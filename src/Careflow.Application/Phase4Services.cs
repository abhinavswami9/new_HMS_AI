using System.Text.Json;
using Careflow.Domain;
namespace Careflow.Application;

// Phase 4 services: insurance/TPA, doctor incentives, telemedicine, inventory/procurement
// and CSSD. Integration boundaries stay explicit — no payer gateway, video provider,
// payment service or procurement marketplace is contacted anywhere in these flows.

public sealed class InsuranceService(IUnitOfWork unitOfWork, IClock clock)
{
    public Task<Coverage> RegisterCoverage(Scope scope, CoverageInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.InsuranceCoverage); await CareGuard.Module(s, "insurance", ct);
        if (await s.FindPatientAsync(input.PatientId, ct) is not { Active: true }) throw new DomainException("insurance.reference", "Select an active patient.");
        if (await s.FindInsurancePlanAsync(input.PlanId, ct) is null) throw new DomainException("insurance.reference", "Select a registered insurance plan.");
        var policy = CareGuard.Text(input.PolicyNumber, 3, 40, "Policy number");
        var member = CareGuard.Text(input.MemberId, 2, 40, "Member ID");
        if (input.ValidTo < input.ValidFrom) throw new DomainException("insurance.coverage", "Coverage end date must not precede the start date.");
        if (input.ValidFrom < scope.Today.AddYears(-10) || input.ValidTo > scope.Today.AddYears(10)) throw new DomainException("insurance.coverage", "Coverage dates must stay within ten years.");
        if (input.SumInsured <= 0) throw new DomainException("insurance.coverage", "Sum insured must be positive.");
        if (input.Deductible < 0 || input.Deductible >= input.SumInsured) throw new DomainException("insurance.coverage", "Deductible must be zero or more and below the sum insured.");
        if (await s.FindCoverageByPolicyAsync(input.PatientId, policy, ct) is not null) throw new DomainException("insurance.duplicate", "This patient already has coverage under the same policy number.");
        var number = await s.NextNumberAsync("COV", $"{scope.BranchId}:{clock.UtcNow.Year}", $"COV-{clock.UtcNow.Year}", ct);
        var coverage = new Coverage(Guid.NewGuid(), number, input.PatientId, input.PlanId, policy, member,
            input.ValidFrom, input.ValidTo, input.SumInsured, input.Deductible, "Active");
        await s.InsertCoverageAsync(coverage, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "insurance.coverage.registered", "Coverage", coverage.Id, policy, new { input.ValidFrom, input.ValidTo, input.SumInsured }), ct);
        await s.AddOutboxAsync("insurance.coverage.registered", coverage.Id, JsonSerializer.Serialize(new { coverage.PatientId, coverage.PolicyNumber }), ct);
        return coverage;
    }, ct);

    public Task<Coverage> VerifyCoverage(Scope scope, Guid id, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.InsuranceRead); await CareGuard.Module(s, "insurance", ct);
        var coverage = await s.FindCoverageAsync(id, ct) ?? throw new DomainException("not_found", "Coverage not found.", 404);
        var eligible = Phase4Rules.CoverageCovers(coverage, scope.Today);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "insurance.eligibility.checked", "Coverage", id,
            reason: eligible ? "Eligible on check date" : "Outside coverage window or inactive", changes: new { On = scope.Today }), ct);
        return coverage;
    }, ct);

    public Task<Preauthorization> RequestPreauth(Scope scope, PreauthInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.InsurancePreauth); await CareGuard.Module(s, "insurance", ct);
        var coverage = await s.FindCoverageAsync(input.CoverageId, ct) ?? throw new DomainException("not_found", "Coverage not found.", 404);
        if (coverage.PatientId != input.PatientId) throw new DomainException("insurance.reference", "The coverage belongs to a different patient.");
        if (!Phase4Rules.CoverageCovers(coverage, scope.Today)) throw new DomainException("insurance.eligibility", "Coverage is not eligible today.");
        var diagnosis = CareGuard.Text(input.DiagnosisCode, 2, 20, "Diagnosis code");
        var procedure = CareGuard.Text(input.ProcedureCode, 2, 20, "Procedure code");
        if (input.RequestedAmount <= 0) throw new DomainException("insurance.preauth", "Requested amount must be positive.");
        if (input.ValidTo < scope.Today || input.ValidTo > scope.Today.AddDays(180)) throw new DomainException("insurance.preauth", "Authorization validity must end within the next 180 days.");
        var number = await s.NextNumberAsync("AUTH", $"{scope.BranchId}:{clock.UtcNow.Year}", $"AUTH-{clock.UtcNow.Year}", ct);
        var preauth = new Preauthorization(Guid.NewGuid(), number, coverage.Id, input.PatientId, diagnosis, procedure,
            input.RequestedAmount, "Requested", scope.Today, input.ValidTo, null, "", null, null, scope.Actor.Id, clock.UtcNow);
        await s.InsertPreauthAsync(preauth, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "insurance.preauth.requested", "Preauthorization", preauth.Id, diagnosis, new { input.RequestedAmount, input.ValidTo }), ct);
        return preauth;
    }, ct);

    public Task<Preauthorization> RespondPreauth(Scope scope, Guid id, PreauthRespondInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.InsuranceRespond); await CareGuard.Module(s, "insurance", ct);
        var preauth = await s.FindPreauthAsync(id, ct) ?? throw new DomainException("not_found", "Preauthorization not found.", 404);
        CareGuard.Version(preauth.Version, input.Version);
        if (preauth.Status != "Requested") throw new DomainException("insurance.state", "Only requested preauthorizations can be answered.", 409);
        if (!new[] { "Approved", "Rejected" }.Contains(input.Decision)) throw new DomainException("insurance.preauth", "Decision must be Approved or Rejected.");
        var note = CareGuard.Text(input.Note, 3, 500, "Payer response note");
        decimal? approved = null;
        if (input.Decision == "Approved")
        {
            approved = input.ApprovedAmount ?? throw new DomainException("insurance.preauth", "Approved amount is required.");
            if (approved <= 0 || approved > preauth.RequestedAmount) throw new DomainException("insurance.preauth", "Approved amount must be positive and not exceed the requested amount.");
        }
        var updated = preauth with { Status = input.Decision, ApprovedAmount = approved, ResponseNote = note, RespondedBy = scope.Actor.Id, RespondedAt = clock.UtcNow, Version = preauth.Version + 1 };
        await s.UpdatePreauthAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, $"insurance.preauth.{input.Decision.ToLowerInvariant()}", "Preauthorization", id, note, new { Approved = approved }), ct);
        await s.AddOutboxAsync("insurance.preauth.responded", id, JsonSerializer.Serialize(new { updated.Status, Approved = approved }), ct);
        return updated;
    }, ct);

    public Task<Claim> CreateClaim(Scope scope, ClaimInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.InsuranceClaim); await CareGuard.Module(s, "insurance", ct);
        if (await s.FindPatientAsync(input.PatientId, ct) is not { Active: true }) throw new DomainException("insurance.reference", "Select an active patient.");
        var coverage = await s.FindCoverageAsync(input.CoverageId, ct) ?? throw new DomainException("not_found", "Coverage not found.", 404);
        if (coverage.PatientId != input.PatientId) throw new DomainException("insurance.reference", "The coverage belongs to a different patient.");
        if (!Phase4Rules.CoverageCovers(coverage, scope.Today)) throw new DomainException("insurance.eligibility", "Coverage is not eligible for a claim today.");
        var diagnosis = CareGuard.Text(input.DiagnosisCode, 2, 20, "Diagnosis code");
        if (input.Lines is not { Count: > 0 and <= 50 }) throw new DomainException("insurance.lines", "A claim needs between 1 and 50 service lines.");
        if (input.InvoiceId is Guid invoiceId)
        {
            if (await s.FindInvoiceAsync(invoiceId, ct) is not { } invoice || invoice.PatientId != input.PatientId) throw new DomainException("insurance.reference", "Linked invoice not found for this patient.");
            if (await s.FindClaimByInvoiceAsync(invoiceId, ct) is not null) throw new DomainException("insurance.duplicate", "A claim already exists for this invoice.");
        }
        var lines = new List<ClaimLine>();
        decimal total = 0;
        foreach (var line in input.Lines)
        {
            var code = CareGuard.Text(line.ServiceCode, 1, 30, "Service code");
            var description = CareGuard.Text(line.Description, 2, 200, "Line description");
            if (line.Quantity is < 1 or > 999) throw new DomainException("insurance.lines", "Line quantity must be between 1 and 999.");
            if (line.UnitPrice < 0) throw new DomainException("insurance.lines", "Line unit price cannot be negative.");
            var amount = Math.Round(line.Quantity * line.UnitPrice, 2);
            total += amount;
            lines.Add(new ClaimLine(Guid.NewGuid(), Guid.Empty, code, description, line.Quantity, line.UnitPrice, amount, 0));
        }
        if (total <= 0) throw new DomainException("insurance.lines", "Claim total must be positive.");
        if (total > coverage.SumInsured) throw new DomainException("insurance.eligibility", "Claim total exceeds the covered sum insured.");
        var number = await s.NextNumberAsync("CLM", $"{scope.BranchId}:{clock.UtcNow.Year}", $"CLM-{clock.UtcNow.Year}", ct);
        var claim = new Claim(Guid.NewGuid(), number, input.PatientId, coverage.Id, input.InvoiceId, input.AdmissionId,
            diagnosis, total, 0, total, "Draft", "", "", "", null, null, scope.Actor.Id, clock.UtcNow);
        await s.InsertClaimAsync(claim, ct);
        foreach (var line in lines) await s.InsertClaimLineAsync(line with { ClaimId = claim.Id }, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "insurance.claim.created", "Claim", claim.Id, diagnosis, new { Total = total, lines.Count }), ct);
        return claim;
    }, ct);

    public Task<Claim> SubmitClaim(Scope scope, Guid id, VersionInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.InsuranceClaim); await CareGuard.Module(s, "insurance", ct);
        var claim = await s.FindClaimAsync(id, ct) ?? throw new DomainException("not_found", "Claim not found.", 404);
        CareGuard.Version(claim.Version, input.Version);
        if (claim.Status is not ("Draft" or "Responded")) throw new DomainException("insurance.state", "Only draft or answered claims can be submitted.", 409);
        if (await s.ListClaimLinesAsync(id, ct) is not { Count: > 0 }) throw new DomainException("insurance.lines", "A claim needs at least one service line.");
        var updated = claim with { Status = "Submitted", SubmittedBy = scope.Actor.Id, SubmittedAt = clock.UtcNow, Version = claim.Version + 1 };
        await s.UpdateClaimAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "insurance.claim.submitted", "Claim", id), ct);
        await s.AddOutboxAsync("insurance.claim.submitted", id, JsonSerializer.Serialize(new { updated.Number, updated.TotalAmount }), ct);
        return updated;
    }, ct);

    public Task<Claim> RaiseQuery(Scope scope, Guid id, ClaimQueryInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.InsuranceRespond); await CareGuard.Module(s, "insurance", ct);
        var claim = await s.FindClaimAsync(id, ct) ?? throw new DomainException("not_found", "Claim not found.", 404);
        if (claim.Status is not ("Submitted" or "Responded")) throw new DomainException("insurance.state", "Queries apply to submitted claims only.", 409);
        var question = CareGuard.Text(input.Question, 5, 1000, "Payer question");
        await s.InsertClaimQueryAsync(new ClaimQuery(Guid.NewGuid(), id, question, null, scope.Actor.Id, clock.UtcNow, null, null), ct);
        var updated = claim with { Status = "Queried", QueryNote = question, Version = claim.Version + 1 };
        await s.UpdateClaimAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "insurance.claim.queried", "Claim", id, question), ct);
        return updated;
    }, ct);

    public Task<Claim> RespondQuery(Scope scope, Guid id, ClaimRespondInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.InsuranceClaim); await CareGuard.Module(s, "insurance", ct);
        var claim = await s.FindClaimAsync(id, ct) ?? throw new DomainException("not_found", "Claim not found.", 404);
        CareGuard.Version(claim.Version, input.Version);
        if (claim.Status != "Queried") throw new DomainException("insurance.state", "Only queried claims can be answered.", 409);
        var response = CareGuard.Text(input.Response, 5, 2000, "Claim response");
        foreach (var query in await s.ListClaimQueriesAsync(id, ct))
            if (query.Response is null) await s.UpdateClaimQueryAsync(query with { Response = response, RespondedBy = scope.Actor.Id, RespondedAt = clock.UtcNow }, ct);
        var updated = claim with { Status = "Responded", ResponseNote = response, Version = claim.Version + 1 };
        await s.UpdateClaimAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "insurance.claim.responded", "Claim", id, response), ct);
        return updated;
    }, ct);

    public Task<Claim> DecideClaim(Scope scope, Guid id, ClaimDecisionInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.InsuranceRespond); await CareGuard.Module(s, "insurance", ct);
        var claim = await s.FindClaimAsync(id, ct) ?? throw new DomainException("not_found", "Claim not found.", 404);
        CareGuard.Version(claim.Version, input.Version);
        if (claim.Status is not ("Submitted" or "Responded")) throw new DomainException("insurance.state", "Only submitted or answered claims can be decided.", 409);
        if (!new[] { "Approved", "Rejected" }.Contains(input.Decision)) throw new DomainException("insurance.state", "Decision must be Approved or Rejected.");
        var reason = CareGuard.Text(input.Reason, 3, 500, "Decision reason");
        Claim updated;
        if (input.Decision == "Approved")
        {
            var approved = input.ApprovedAmount ?? throw new DomainException("insurance.state", "Approved amount is required.");
            if (approved <= 0 || approved > claim.TotalAmount) throw new DomainException("insurance.state", "Approved amount must be positive and not exceed the claim total.");
            updated = claim with { Status = "Approved", ApprovedAmount = approved, PatientLiability = claim.TotalAmount - approved, ResponseNote = reason, Version = claim.Version + 1 };
        }
        else
        {
            updated = claim with { Status = "Rejected", RejectionReason = reason, PatientLiability = claim.TotalAmount, Version = claim.Version + 1 };
        }
        await s.UpdateClaimAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, $"insurance.claim.{input.Decision.ToLowerInvariant()}", "Claim", id, reason, new { updated.ApprovedAmount, updated.PatientLiability }), ct);
        await s.AddOutboxAsync("insurance.claim.decided", id, JsonSerializer.Serialize(new { updated.Status, updated.ApprovedAmount }), ct);
        return updated;
    }, ct);

    public Task<Settlement> SettleClaim(Scope scope, Guid id, SettlementInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.InsuranceSettle); await CareGuard.Module(s, "insurance", ct);
        var claim = await s.FindClaimAsync(id, ct) ?? throw new DomainException("not_found", "Claim not found.", 404);
        CareGuard.Version(claim.Version, input.Version);
        if (claim.Status is not ("Approved" or "Settled")) throw new DomainException("insurance.state", "Only approved claims can be settled.", 409);
        if (input.Amount <= 0) throw new DomainException("insurance.settlement", "Settlement amount must be positive.");
        if (!Phase4Rules.SettlementModes.Contains(input.Mode)) throw new DomainException("insurance.settlement", "Select a valid settlement mode.");
        var reference = CareGuard.Text(input.Reference, 3, 60, "Settlement reference");
        if (await s.FindSettlementByReferenceAsync(reference, ct) is not null) throw new DomainException("insurance.duplicate", "This settlement reference has already been recorded.");
        var settledSoFar = (await s.ListSettlementsAsync(id, ct)).Sum(x => x.Amount);
        if (settledSoFar + input.Amount > claim.ApprovedAmount) throw new DomainException("insurance.settlement", "Settlements cannot exceed the approved amount.");
        var number = await s.NextNumberAsync("SETL", $"{scope.BranchId}:{clock.UtcNow.Year}", $"SETL-{clock.UtcNow.Year}", ct);
        var settlement = new Settlement(Guid.NewGuid(), number, id, input.Amount, input.Mode, reference, clock.UtcNow, scope.Actor.Id);
        await s.InsertSettlementAsync(settlement, ct);
        if (settledSoFar + input.Amount >= claim.ApprovedAmount)
            await s.UpdateClaimAsync(claim with { Status = "Settled", Version = claim.Version + 1 }, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "insurance.claim.settled", "Claim", id, reference, new { input.Amount, input.Mode }), ct);
        await s.AddOutboxAsync("insurance.claim.settled", id, JsonSerializer.Serialize(new { settlement.Number, settlement.Amount }), ct);
        return settlement;
    }, ct);

    public Task<IReadOnlyList<Coverage>> ListCoverages(Scope scope, Guid? patientId, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.InsuranceRead); await CareGuard.Module(s, "insurance", ct);
        return await s.ListCoveragesAsync(patientId, ct);
    }, ct);
    public Task<IReadOnlyList<Preauthorization>> ListPreauths(Scope scope, Guid? patientId, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.InsuranceRead); await CareGuard.Module(s, "insurance", ct);
        return await s.ListPreauthsAsync(patientId, ct);
    }, ct);
    public Task<IReadOnlyList<Claim>> ListClaims(Scope scope, Guid? patientId, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.InsuranceRead); await CareGuard.Module(s, "insurance", ct);
        return await s.ListClaimsAsync(patientId, ct);
    }, ct);
}

public sealed class IncentiveService(IUnitOfWork unitOfWork, IClock clock)
{
    private static async Task<IncentiveRule?> SelectRuleAsync(IHospitalStore s, IncentiveEvent evt, CancellationToken ct)
    {
        var rules = await s.ListActiveIncentiveRulesAsync(DateOnly.FromDateTime(evt.At.UtcDateTime), ct);
        return rules.Where(r => r.Basis == evt.Basis && (r.ScopeKind == "All" || r.DoctorId == evt.DoctorId))
            .OrderByDescending(r => r.ScopeKind == "Doctor").FirstOrDefault();
    }

    public Task<IncentiveRule> CreateRule(Scope scope, IncentiveRuleInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.IncentiveRule); await CareGuard.Module(s, "incentives", ct);
        var name = CareGuard.Text(input.Name, 3, 120, "Rule name");
        if (!Phase4Rules.IncentiveScopes.Contains(input.ScopeKind)) throw new DomainException("incentive.fields", "Rule scope must be Doctor or All.");
        if (input.ScopeKind == "Doctor")
        {
            if (input.DoctorId is not Guid doctorId || !await s.HasActivePractitionerAsync(doctorId, ct)) throw new DomainException("incentive.fields", "Doctor scope requires an active practitioner.");
        }
        if (!Phase4Rules.IncentiveBases.Contains(input.Basis)) throw new DomainException("incentive.fields", "Select a valid eligible basis.");
        if (!Phase4Rules.IncentiveModes.Contains(input.Mode)) throw new DomainException("incentive.fields", "Incentive mode must be Fixed or Percent.");
        if (input.Mode == "Percent" && input.Basis != "Revenue") throw new DomainException("incentive.fields", "Percentage rules apply to the Revenue basis only.");
        if (input.Mode == "Percent" ? input.Value is < 0 or > 100 : input.Value < 0) throw new DomainException("incentive.fields", "Percent values must be 0–100 and fixed amounts cannot be negative.");
        if (input.EffectiveTo is DateOnly to && to < input.EffectiveFrom) throw new DomainException("incentive.fields", "Effective end date must not precede the start date.");
        if (await s.HasOverlappingIncentiveRuleAsync(input.ScopeKind, input.DoctorId, input.Basis, input.EffectiveFrom, input.EffectiveTo, null, ct))
            throw new DomainException("incentive.overlap", "An ambiguous rule already covers this scope, basis and period.");
        var rule = new IncentiveRule(Guid.NewGuid(), name, input.ScopeKind, input.DoctorId, input.Basis, input.Mode,
            input.Value, input.EffectiveFrom, input.EffectiveTo, "Draft", scope.Actor.Id);
        await s.InsertIncentiveRuleAsync(rule, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "incentive.rule.created", "IncentiveRule", rule.Id, name, new { input.ScopeKind, input.Basis, input.Mode, input.Value }), ct);
        return rule;
    }, ct);

    public Task<IncentiveRule> SetRuleStatus(Scope scope, Guid id, string status, VersionInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.IncentiveRule); await CareGuard.Module(s, "incentives", ct);
        var rule = await s.FindIncentiveRuleAsync(id, ct) ?? throw new DomainException("not_found", "Incentive rule not found.", 404);
        CareGuard.Version(rule.Version, input.Version);
        var allowed = (rule.Status, status) switch
        {
            ("Draft", "Active") or ("Active", "Retired") => true,
            _ => false
        };
        if (!allowed) throw new DomainException("incentive.state", "Rules move Draft → Active → Retired only.", 409);
        var updated = rule with { Status = status, Version = rule.Version + 1 };
        await s.UpdateIncentiveRuleAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, $"incentive.rule.{status.ToLowerInvariant()}", "IncentiveRule", id, input.Reason), ct);
        return updated;
    }, ct);

    public Task<IncentiveBatch> Calculate(Scope scope, IncentiveBatchInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.IncentiveCalculate); await CareGuard.Module(s, "incentives", ct);
        if (input.PeriodEnd < input.PeriodStart) throw new DomainException("incentive.period", "Period end must not precede the start.");
        if (input.PeriodEnd >= scope.Today) throw new DomainException("incentive.period", "Calculate incentives for closed periods only.");
        if (input.PeriodStart < input.PeriodEnd.AddYears(-2)) throw new DomainException("incentive.period", "Period must be within two years.");
        if (await s.FindIncentiveBatchByPeriodAsync(input.PeriodStart, input.PeriodEnd, ct) is not null) throw new DomainException("incentive.period", "A batch already exists for this exact period.");
        var events = await s.ListIncentiveEventsAsync(input.PeriodStart, input.PeriodEnd, ct);
        var number = await s.NextNumberAsync("INC", $"{scope.BranchId}:{clock.UtcNow.Year}", $"INC-{clock.UtcNow.Year}", ct);
        var batch = new IncentiveBatch(Guid.NewGuid(), number, input.PeriodStart, input.PeriodEnd, "Calculated", 0, scope.Actor.Name, clock.UtcNow, "", null, null);
        await s.InsertIncentiveBatchAsync(batch, ct);
        decimal total = 0;
        foreach (var evt in events)
        {
            var rule = await SelectRuleAsync(s, evt, ct);
            if (rule is null) continue;
            var amount = rule.Mode == "Fixed" ? rule.Value : Math.Round(evt.Amount * rule.Value / 100, 2);
            if (amount <= 0) continue;
            total += amount;
            await s.InsertIncentiveLineAsync(new IncentiveLine(Guid.NewGuid(), batch.Id, evt.DoctorId, rule.Id,
                $"{evt.Basis}:{evt.Ref}", evt.Amount, amount, $"ref={evt.Ref};amount={evt.Amount};rule={rule.Name};mode={rule.Mode};value={rule.Value}"), ct);
        }
        var updated = batch with { TotalAmount = total, Version = batch.Version + 1 };
        await s.UpdateIncentiveBatchAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "incentive.batch.calculated", "IncentiveBatch", batch.Id, reason: $"{events.Count} eligible events", changes: new { Total = total }), ct);
        return updated;
    }, ct);

    public Task<IncentiveBatch> Recalculate(Scope scope, Guid id, BatchRecalcInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.IncentiveCalculate); await CareGuard.Module(s, "incentives", ct);
        var batch = await s.FindIncentiveBatchAsync(id, ct) ?? throw new DomainException("not_found", "Incentive batch not found.", 404);
        CareGuard.Version(batch.Version, input.Version);
        if (batch.Status != "Calculated") throw new DomainException("incentive.state", "Only calculated (unapproved) batches can be recalculated.", 409);
        var reason = CareGuard.Text(input.Reason, 5, 500, "Recalculation reason");
        await s.DeleteIncentiveLinesAsync(id, ct);
        var events = await s.ListIncentiveEventsAsync(batch.PeriodStart, batch.PeriodEnd, ct);
        decimal total = 0;
        foreach (var evt in events)
        {
            var rule = await SelectRuleAsync(s, evt, ct);
            if (rule is null) continue;
            var amount = rule.Mode == "Fixed" ? rule.Value : Math.Round(evt.Amount * rule.Value / 100, 2);
            if (amount <= 0) continue;
            total += amount;
            await s.InsertIncentiveLineAsync(new IncentiveLine(Guid.NewGuid(), batch.Id, evt.DoctorId, rule.Id,
                $"{evt.Basis}:{evt.Ref}", evt.Amount, amount, $"ref={evt.Ref};amount={evt.Amount};rule={rule.Name};mode={rule.Mode};value={rule.Value}"), ct);
        }
        var updated = batch with { TotalAmount = total, RecalculationReason = reason, Version = batch.Version + 1 };
        await s.UpdateIncentiveBatchAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "incentive.batch.recalculated", "IncentiveBatch", id, reason, new { Total = total }), ct);
        return updated;
    }, ct);

    public Task<IncentiveBatch> Approve(Scope scope, Guid id, BatchApproveInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.IncentiveApprove); await CareGuard.Module(s, "incentives", ct);
        var batch = await s.FindIncentiveBatchAsync(id, ct) ?? throw new DomainException("not_found", "Incentive batch not found.", 404);
        CareGuard.Version(batch.Version, input.Version);
        if (batch.Status != "Calculated") throw new DomainException("incentive.state", "Only calculated batches can be approved.", 409);
        if (batch.CalculatedBy == scope.Actor.Name) throw new DomainException("incentive.separation", "A second authorized person must approve the calculation.", 403);
        var reason = CareGuard.Text(input.Reason, 3, 500, "Approval reason");
        var updated = batch with { Status = "Approved", ApprovedBy = scope.Actor.Name, ApprovedAt = clock.UtcNow, Version = batch.Version + 1 };
        await s.UpdateIncentiveBatchAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "incentive.batch.approved", "IncentiveBatch", id, reason, new { updated.TotalAmount }), ct);
        await s.AddOutboxAsync("incentive.batch.approved", id, JsonSerializer.Serialize(new { updated.Number, updated.TotalAmount }), ct);
        return updated;
    }, ct);

    public Task<IncentivePayment> Pay(Scope scope, Guid id, BatchPayInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.IncentivePay); await CareGuard.Module(s, "incentives", ct);
        var batch = await s.FindIncentiveBatchAsync(id, ct) ?? throw new DomainException("not_found", "Incentive batch not found.", 404);
        CareGuard.Version(batch.Version, input.Version);
        if (batch.Status is not ("Approved" or "Paid")) throw new DomainException("incentive.state", "Only approved batches can be paid.", 409);
        if (input.Amount <= 0) throw new DomainException("incentive.payment", "Payment amount must be positive.");
        if (!Phase4Rules.SettlementModes.Contains(input.Mode)) throw new DomainException("incentive.payment", "Select a valid payment mode.");
        var reference = CareGuard.Text(input.Reference, 3, 60, "Payment reference");
        var paidSoFar = (await s.ListIncentivePaymentsAsync(id, ct)).Sum(p => p.Amount);
        if (paidSoFar + input.Amount > batch.TotalAmount) throw new DomainException("incentive.payment", "Payments cannot exceed the approved batch total.");
        var payment = new IncentivePayment(Guid.NewGuid(), id, input.Amount, input.Mode, reference, scope.Actor.Id, clock.UtcNow);
        await s.InsertIncentivePaymentAsync(payment, ct);
        if (paidSoFar + input.Amount >= batch.TotalAmount)
            await s.UpdateIncentiveBatchAsync(batch with { Status = "Paid", Version = batch.Version + 1 }, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "incentive.payment.recorded", "IncentiveBatch", id, reference, new { input.Amount, input.Mode }), ct);
        return payment;
    }, ct);

    public Task<IReadOnlyList<IncentiveRule>> ListRules(Scope scope, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.IncentiveRead); await CareGuard.Module(s, "incentives", ct);
        return await s.ListIncentiveRulesAsync(ct);
    }, ct);
    public Task<IReadOnlyList<IncentiveBatch>> ListBatches(Scope scope, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.IncentiveRead); await CareGuard.Module(s, "incentives", ct);
        return await s.ListIncentiveBatchesAsync(ct);
    }, ct);
    public Task<IReadOnlyList<IncentiveLine>> ListLines(Scope scope, Guid batchId, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.IncentiveRead); await CareGuard.Module(s, "incentives", ct);
        return await s.ListIncentiveLinesAsync(batchId, ct);
    }, ct);
}

public sealed class TelemedicineService(IUnitOfWork unitOfWork, IClock clock)
{
    public Task<TeleSession> Book(Scope scope, TeleSessionInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.TeleSchedule); await CareGuard.Module(s, "telemedicine", ct);
        var appointment = await s.FindAppointmentAsync(input.AppointmentId, ct) ?? throw new DomainException("not_found", "Appointment not found.", 404);
        if (appointment.Status == "Cancelled") throw new DomainException("tele.state", "Cancelled appointments cannot host tele sessions.", 409);
        if (await s.FindTeleSessionByAppointmentAsync(appointment.Id, ct) is not null) throw new DomainException("tele.duplicate", "A tele session already exists for this appointment.");
        var number = await s.NextNumberAsync("TEL", $"{scope.BranchId}:{clock.UtcNow.Year}", $"TEL-{clock.UtcNow.Year}", ct);
        var session = new TeleSession(Guid.NewGuid(), number, appointment.Id, appointment.PatientId, appointment.DoctorId,
            "Scheduled", appointment.StartsAt, false, null, null, "", "");
        await s.InsertTeleSessionAsync(session, ct);
        await s.UpdateAppointmentAsync(appointment with { Mode = "Telemedicine", Version = appointment.Version + 1 }, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "tele.session.booked", "TeleSession", session.Id, number, new { appointment.StartsAt }), ct);
        return session;
    }, ct);

    public Task<TeleSession> RecordConsent(Scope scope, Guid id, TeleConsentInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.TeleRead); await CareGuard.Module(s, "telemedicine", ct);
        var session = await s.FindTeleSessionAsync(id, ct) ?? throw new DomainException("not_found", "Tele session not found.", 404);
        if (session.Status is "Completed" or "Cancelled") throw new DomainException("tele.state", "This session is closed.", 409);
        if (!new[] { "PatientConsent", "RecordingConsent" }.Contains(input.Kind)) throw new DomainException("tele.consent", "Consent kind must be PatientConsent or RecordingConsent.");
        var note = CareGuard.Text(input.Note, 0, 500, "Consent note");
        await s.InsertTeleConsentEventAsync(new TeleConsentEvent(Guid.NewGuid(), id, input.Kind, input.Given, note, scope.Actor.Id, clock.UtcNow), ct);
        TeleSession updated = session;
        if (input.Kind == "PatientConsent" && input.Given && !session.ConsentObtained)
        {
            updated = session with { ConsentObtained = true, Version = session.Version + 1 };
            await s.UpdateTeleSessionAsync(updated, ct);
        }
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "tele.consent.recorded", "TeleSession", id, note, new { input.Kind, input.Given }), ct);
        return updated;
    }, ct);

    public Task<TeleSession> JoinWaiting(Scope scope, Guid id, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.TeleRead); await CareGuard.Module(s, "telemedicine", ct);
        var session = await s.FindTeleSessionAsync(id, ct) ?? throw new DomainException("not_found", "Tele session not found.", 404);
        if (session.Status != "Scheduled") throw new DomainException("tele.state", "Only scheduled sessions accept waiting-room joins.", 409);
        if (!session.ConsentObtained) throw new DomainException("tele.consent", "Record patient consent before joining the waiting room.");
        await s.InsertTeleWaitingEventAsync(new TeleWaitingEvent(Guid.NewGuid(), id, "JoinedWaiting", scope.Actor.Id, clock.UtcNow), ct);
        var updated = session with { Status = "Waiting", Version = session.Version + 1 };
        await s.UpdateTeleSessionAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "tele.waiting.joined", "TeleSession", id), ct);
        return updated;
    }, ct);

    public Task<TeleJoin> IssueJoinToken(Scope scope, Guid id, VersionInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.TeleConsult); await CareGuard.Module(s, "telemedicine", ct);
        var session = await s.FindTeleSessionAsync(id, ct) ?? throw new DomainException("not_found", "Tele session not found.", 404);
        CareGuard.Version(session.Version, input.Version);
        if (session.Status is not ("Waiting" or "InConsultation")) throw new DomainException("tele.state", "Join tokens issue for waiting or in-consultation sessions only.", 409);
        if (!session.ConsentObtained) throw new DomainException("tele.consent", "Patient consent is required before any join token issues.");
        if (clock.UtcNow < session.ScheduledAt.AddHours(-24) || clock.UtcNow > session.ScheduledAt.AddHours(24)) throw new DomainException("tele.window", "Join tokens issue within 24 hours of the scheduled time.");
        // Ephemeral authorization credential for the waiting-room workflow. No live video
        // provider is connected (see docs/PHASE4_STATUS.md) — the token is opaque, expiring
        // and recorded in the audit trail.
        var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var expires = clock.UtcNow.AddMinutes(5);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "tele.join_token.issued", "TeleSession", id, reason: "Ephemeral preview-room token", changes: new { Expires = expires }), ct);
        return new TeleJoin(id, token, expires);
    }, ct);

    public Task<TeleSession> Admit(Scope scope, Guid id, VersionInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.TeleConsult); await CareGuard.Module(s, "telemedicine", ct);
        var session = await s.FindTeleSessionAsync(id, ct) ?? throw new DomainException("not_found", "Tele session not found.", 404);
        CareGuard.Version(session.Version, input.Version);
        if (session.Status != "Waiting") throw new DomainException("tele.state", "Only waiting sessions can be admitted.", 409);
        if (!session.ConsentObtained) throw new DomainException("tele.consent", "Patient consent is required before admission.");
        await s.InsertTeleWaitingEventAsync(new TeleWaitingEvent(Guid.NewGuid(), id, "Admitted", scope.Actor.Id, clock.UtcNow), ct);
        var updated = session with { Status = "InConsultation", StartedAt = clock.UtcNow, Version = session.Version + 1 };
        await s.UpdateTeleSessionAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "tele.session.admitted", "TeleSession", id), ct);
        return updated;
    }, ct);

    public Task<TeleSession> End(Scope scope, Guid id, TeleEndInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.TeleConsult); await CareGuard.Module(s, "telemedicine", ct);
        var session = await s.FindTeleSessionAsync(id, ct) ?? throw new DomainException("not_found", "Tele session not found.", 404);
        CareGuard.Version(session.Version, input.Version);
        if (session.Status != "InConsultation") throw new DomainException("tele.state", "Only in-consultation sessions can end.", 409);
        var note = CareGuard.Text(input.Note, 5, 2000, "Consultation note");
        var advice = CareGuard.Text(input.EmergencyAdvice, 5, 1000, "Emergency escalation advice");
        var updated = session with { Status = "Completed", EndedAt = clock.UtcNow, EndNote = note, EmergencyAdvice = advice, Version = session.Version + 1 };
        await s.UpdateTeleSessionAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "tele.session.ended", "TeleSession", id, note, new { advice }), ct);
        await s.AddOutboxAsync("tele.session.ended", id, JsonSerializer.Serialize(new { updated.Number, updated.Status }), ct);
        return updated;
    }, ct);

    public Task<TeleUpload> Upload(Scope scope, Guid id, TeleUploadInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.TeleUpload); await CareGuard.Module(s, "telemedicine", ct);
        var session = await s.FindTeleSessionAsync(id, ct) ?? throw new DomainException("not_found", "Tele session not found.", 404);
        if (session.Status == "Cancelled") throw new DomainException("tele.state", "Cancelled sessions do not accept uploads.", 409);
        var fileName = input.FileName.Trim();
        if (fileName.Length is < 3 or > 120 || fileName.Contains('/') || fileName.Contains('\\') || fileName.Contains("..")) throw new DomainException("tele.upload", "File name must be a plain name of 3–120 characters.");
        var extension = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
        if (!Phase4Rules.UploadExtensions.Contains(extension)) throw new DomainException("tele.upload", $"Allowed document types: {string.Join(", ", Phase4Rules.UploadExtensions)}.");
        if (input.SizeBytes is < 1 or > Phase4Rules.UploadMaxBytes) throw new DomainException("tele.upload", "Uploads must be between 1 byte and 10 MB.");
        if (string.IsNullOrWhiteSpace(input.ContentType)) throw new DomainException("tele.upload", "Content type is required.");
        var upload = new TeleUpload(Guid.NewGuid(), id, fileName, input.ContentType.Trim(), input.SizeBytes, scope.Actor.Id, clock.UtcNow);
        await s.InsertTeleUploadAsync(upload, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "tele.document.uploaded", "TeleSession", id, fileName, new { input.SizeBytes, input.ContentType }), ct);
        return upload;
    }, ct);

    public Task<TeleSession> Cancel(Scope scope, Guid id, VersionInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.TeleSchedule); await CareGuard.Module(s, "telemedicine", ct);
        var session = await s.FindTeleSessionAsync(id, ct) ?? throw new DomainException("not_found", "Tele session not found.", 404);
        CareGuard.Version(session.Version, input.Version);
        if (session.Status is not ("Scheduled" or "Waiting")) throw new DomainException("tele.state", "Only scheduled or waiting sessions can be cancelled.", 409);
        var updated = session with { Status = "Cancelled", EndedAt = clock.UtcNow, Version = session.Version + 1 };
        await s.UpdateTeleSessionAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "tele.session.cancelled", "TeleSession", id, input.Reason), ct);
        return updated;
    }, ct);

    public Task<IReadOnlyList<TeleSession>> ListSessions(Scope scope, Guid? patientId, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.TeleRead); await CareGuard.Module(s, "telemedicine", ct);
        return await s.ListTeleSessionsAsync(patientId, ct);
    }, ct);
}

public sealed class InventoryService(IUnitOfWork unitOfWork, IClock clock)
{
    private static InventoryBatch Guard(InventoryBatch batch, int delta)
    {
        if (batch.Quantity + delta < 0) throw new DomainException("inventory.stock", "Insufficient stock in the source batch.", 409);
        return batch;
    }

    public Task<InventoryIndent> RaiseIndent(Scope scope, IndentInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.InventoryIndent); await CareGuard.Module(s, "inventory", ct);
        if (await s.FindInventoryStoreAsync(input.StoreId, ct) is null) throw new DomainException("inventory.reference", "Select a valid store.");
        if (await s.FindInventoryItemAsync(input.ItemId, ct) is not { Active: true }) throw new DomainException("inventory.reference", "Select an active inventory item.");
        if (input.Quantity is < 1 or > 100000) throw new DomainException("inventory.indent", "Indent quantity must be between 1 and 100000.");
        if (!Phase4Rules.IndentPriorities.Contains(input.Priority)) throw new DomainException("inventory.indent", "Select a valid priority.");
        var reason = CareGuard.Text(input.Reason, 3, 500, "Indent reason");
        var number = await s.NextNumberAsync("IND", $"{scope.BranchId}:{clock.UtcNow.Year}", $"IND-{clock.UtcNow.Year}", ct);
        var indent = new InventoryIndent(Guid.NewGuid(), number, input.StoreId, input.ItemId, input.Quantity,
            input.Priority, "Requested", reason, scope.Actor.Id, clock.UtcNow, null, null);
        await s.InsertInventoryIndentAsync(indent, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "inventory.indent.raised", "InventoryIndent", indent.Id, reason, new { input.Quantity, input.Priority }), ct);
        return indent;
    }, ct);

    public Task<InventoryIndent> DecideIndent(Scope scope, Guid id, IndentDecisionInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.InventoryApprove); await CareGuard.Module(s, "inventory", ct);
        var indent = await s.FindInventoryIndentAsync(id, ct) ?? throw new DomainException("not_found", "Indent not found.", 404);
        CareGuard.Version(indent.Version, input.Version);
        if (indent.Status != "Requested") throw new DomainException("inventory.state", "Only requested indents can be decided.", 409);
        if (indent.RaisedBy == scope.Actor.Id) throw new DomainException("inventory.separation", "A second authorized person must decide this indent.", 403);
        var note = CareGuard.Text(input.Note, 3, 500, "Decision note");
        var updated = indent with
        {
            Status = input.Approve ? "Approved" : "Rejected",
            ApprovedBy = scope.Actor.Id,
            ApprovedAt = clock.UtcNow,
            Version = indent.Version + 1
        };
        await s.UpdateInventoryIndentAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, $"inventory.indent.{(input.Approve ? "approved" : "rejected")}", "InventoryIndent", id, note), ct);
        return updated;
    }, ct);

    public Task<PurchaseOrder> CreatePO(Scope scope, PurchaseOrderInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.InventoryPurchase); await CareGuard.Module(s, "inventory", ct);
        var indent = await s.FindInventoryIndentAsync(input.IndentId, ct) ?? throw new DomainException("not_found", "Indent not found.", 404);
        if (await s.FindPurchaseOrderByIndentAsync(indent.Id, ct) is not null) throw new DomainException("inventory.duplicate", "This indent already has a purchase order.");
        if (indent.Status != "Approved") throw new DomainException("inventory.state", "Purchase orders require an approved indent.", 409);
        if (input.Quantity < 1 || input.Quantity > indent.Quantity) throw new DomainException("inventory.tolerance", "Purchase quantity must be between 1 and the indented quantity.");
        if (input.UnitCost < 0) throw new DomainException("inventory.tolerance", "Unit cost cannot be negative.");
        var vendorName = CareGuard.Text(input.VendorName, 2, 120, "Vendor");
        var vendor = await s.FindVendorByNameAsync(vendorName, ct) ?? throw new DomainException("inventory.vendor", "Order from an authorized vendor from the register.");
        if (!vendor.Authorized) throw new DomainException("inventory.vendor", "This vendor is not authorized for purchasing.");
        var number = await s.NextNumberAsync("PO", $"{scope.BranchId}:{clock.UtcNow.Year}", $"PO-{clock.UtcNow.Year}", ct);
        var order = new PurchaseOrder(Guid.NewGuid(), number, vendor.Id, indent.ItemId, indent.Id, input.Quantity,
            input.UnitCost, "Issued", scope.Actor.Id, clock.UtcNow, null, null);
        await s.InsertPurchaseOrderAsync(order, ct);
        await s.UpdateInventoryIndentAsync(indent with { Status = "Ordered", Version = indent.Version + 1 }, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "inventory.po.issued", "PurchaseOrder", order.Id, vendorName, new { input.Quantity, input.UnitCost }), ct);
        return order;
    }, ct);

    public Task<GoodsReceipt> ReceiveGoods(Scope scope, Guid poId, GoodsReceiptInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.InventoryReceive); await CareGuard.Module(s, "inventory", ct);
        var order = await s.FindPurchaseOrderAsync(poId, ct) ?? throw new DomainException("not_found", "Purchase order not found.", 404);
        CareGuard.Version(order.Version, input.Version);
        if (await s.FindGoodsReceiptByPoAsync(poId, ct) is not null) throw new DomainException("inventory.duplicate", "This purchase order already has a goods receipt.");
        if (order.Status != "Issued") throw new DomainException("inventory.state", "Only issued purchase orders can receive goods.", 409);
        if (input.ReceivedQty < 1) throw new DomainException("inventory.tolerance", "Received quantity must be positive.");
        if (input.AcceptedQty < 0 || input.RejectedQty < 0 || input.AcceptedQty + input.RejectedQty != input.ReceivedQty) throw new DomainException("inventory.tolerance", "Accepted plus rejected quantity must equal the received quantity.");
        if (input.AcceptedQty > Math.Ceiling(order.Quantity * 1.05m)) throw new DomainException("inventory.tolerance", "Accepted quantity exceeds the ordered quantity plus 5% tolerance.");
        var inspection = input.RejectedQty > 0 ? CareGuard.Text(input.InspectionNote, 3, 500, "Inspection note") : input.InspectionNote.Trim();
        var batchNumber = CareGuard.Text(input.BatchNumber, 2, 40, "Batch number");
        if (input.ExpiryDate is DateOnly expiry && expiry <= scope.Today) throw new DomainException("inventory.expiry", "Received batches must not be expired.");
        var indent = await s.FindInventoryIndentAsync(order.IndentId, ct) ?? throw new DomainException("not_found", "Linked indent not found.", 404);
        var storeId = indent.StoreId;
        if (await s.FindInventoryBatchByNumberAsync(order.ItemId, batchNumber, storeId, ct) is not null) throw new DomainException("inventory.duplicate", "A batch with this number already exists for the item in the receiving store.");
        var number = await s.NextNumberAsync("GRN", $"{scope.BranchId}:{clock.UtcNow.Year}", $"GRN-{clock.UtcNow.Year}", ct);
        var receipt = new GoodsReceipt(Guid.NewGuid(), number, poId, input.ReceivedQty, input.AcceptedQty, input.RejectedQty,
            inspection, scope.Actor.Id, clock.UtcNow);
        await s.InsertGoodsReceiptAsync(receipt, ct);
        if (input.AcceptedQty > 0)
        {
            var item = await s.FindInventoryItemAsync(order.ItemId, ct) ?? throw new DomainException("not_found", "Item not found.", 404);
            var batch = new InventoryBatch(Guid.NewGuid(), batchNumber, order.ItemId, storeId, input.ExpiryDate, input.AcceptedQty, order.UnitCost);
            await s.InsertInventoryBatchAsync(batch, ct);
            await s.InsertInventoryMovementAsync(new InventoryMovement(Guid.NewGuid(), order.ItemId, storeId, batch.Id,
                "Receipt", input.AcceptedQty, "GoodsReceipt", receipt.Id.ToString(), scope.Actor.Id, clock.UtcNow), ct);
            if (item.Category == "Asset")
            {
                for (var i = 1; i <= input.AcceptedQty; i++)
                {
                    var serial = $"{batchNumber}-{i:D3}";
                    if (await s.FindAssetBySerialAsync(serial, ct) is not null) throw new DomainException("inventory.duplicate", $"Serial number {serial} is already on the asset register.");
                    await s.InsertAssetAsync(new Asset(Guid.NewGuid(), $"{item.Code}-{serial}", order.ItemId, serial, "InStore", storeId.ToString(), scope.Actor.Id, clock.UtcNow), ct);
                }
            }
        }
        await s.UpdatePurchaseOrderAsync(order with { Status = "Received", ReceivedBy = scope.Actor.Id, ReceivedAt = clock.UtcNow, Version = order.Version + 1 }, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "inventory.goods.received", "PurchaseOrder", poId, inspection, new { input.ReceivedQty, input.AcceptedQty, input.RejectedQty, Batch = batchNumber }), ct);
        return receipt;
    }, ct);

    public Task<InventoryTransfer> DispatchTransfer(Scope scope, StockTransferInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.InventoryTransfer); await CareGuard.Module(s, "inventory", ct);
        if (await s.FindInventoryStoreAsync(input.FromStoreId, ct) is null || await s.FindInventoryStoreAsync(input.ToStoreId, ct) is null) throw new DomainException("inventory.reference", "Select valid source and destination stores.");
        if (input.FromStoreId == input.ToStoreId) throw new DomainException("inventory.reference", "Source and destination stores must differ.");
        var batch = await s.FindInventoryBatchAsync(input.BatchId, ct) ?? throw new DomainException("not_found", "Batch not found.", 404);
        if (batch.ItemId != input.ItemId || batch.StoreId != input.FromStoreId) throw new DomainException("inventory.reference", "The batch does not belong to the source store and item.");
        if (input.Quantity is < 1 or > 100000) throw new DomainException("inventory.stock", "Transfer quantity must be between 1 and 100000.");
        Guard(batch, -input.Quantity);
        var note = CareGuard.Text(input.Note, 0, 500, "Transfer note");
        var number = await s.NextNumberAsync("TRF", $"{scope.BranchId}:{clock.UtcNow.Year}", $"TRF-{clock.UtcNow.Year}", ct);
        var transfer = new InventoryTransfer(Guid.NewGuid(), number, input.FromStoreId, input.ToStoreId, input.ItemId,
            input.BatchId, input.Quantity, "Dispatched", note, scope.Actor.Id, clock.UtcNow, null, null);
        await s.InsertTransferAsync(transfer, ct);
        await s.UpdateInventoryBatchAsync(batch with { Quantity = batch.Quantity - input.Quantity, Version = batch.Version + 1 }, ct);
        await s.InsertInventoryMovementAsync(new InventoryMovement(Guid.NewGuid(), input.ItemId, input.FromStoreId, batch.Id,
            "Transfer", -input.Quantity, "Transfer", transfer.Id.ToString(), scope.Actor.Id, clock.UtcNow), ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "inventory.transfer.dispatched", "InventoryTransfer", transfer.Id, note, new { input.Quantity }), ct);
        return transfer;
    }, ct);

    public Task<InventoryTransfer> ReceiveTransfer(Scope scope, Guid id, TransferReceiveInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.InventoryTransfer); await CareGuard.Module(s, "inventory", ct);
        var transfer = await s.FindTransferAsync(id, ct) ?? throw new DomainException("not_found", "Transfer not found.", 404);
        CareGuard.Version(transfer.Version, input.Version);
        if (transfer.Status != "Dispatched") throw new DomainException("inventory.state", "Only dispatched transfers can be received.", 409);
        var source = await s.FindInventoryBatchAsync(transfer.BatchId, ct) ?? throw new DomainException("not_found", "Source batch not found.", 404);
        var destination = await s.FindInventoryBatchByNumberAsync(transfer.ItemId, source.BatchNumber, transfer.ToStoreId, ct);
        if (destination is null)
        {
            destination = new InventoryBatch(Guid.NewGuid(), source.BatchNumber, transfer.ItemId, transfer.ToStoreId, source.ExpiryDate, 0, source.UnitCost);
            await s.InsertInventoryBatchAsync(destination, ct);
        }
        await s.UpdateInventoryBatchAsync(destination with { Quantity = destination.Quantity + transfer.Quantity, Version = destination.Version + 1 }, ct);
        await s.InsertInventoryMovementAsync(new InventoryMovement(Guid.NewGuid(), transfer.ItemId, transfer.ToStoreId, destination.Id,
            "Transfer", transfer.Quantity, "Transfer", transfer.Id.ToString(), scope.Actor.Id, clock.UtcNow), ct);
        var updated = transfer with { Status = "Received", ReceivedBy = scope.Actor.Id, ReceivedAt = clock.UtcNow, Version = transfer.Version + 1 };
        await s.UpdateTransferAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "inventory.transfer.received", "InventoryTransfer", id), ct);
        return updated;
    }, ct);

    public Task<InventoryConsumption> Consume(Scope scope, ConsumptionInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.InventoryConsume); await CareGuard.Module(s, "inventory", ct);
        if (await s.FindInventoryStoreAsync(input.StoreId, ct) is null) throw new DomainException("inventory.reference", "Select a valid store.");
        var batch = await s.FindInventoryBatchAsync(input.BatchId, ct) ?? throw new DomainException("not_found", "Batch not found.", 404);
        if (batch.StoreId != input.StoreId || batch.ItemId != input.ItemId) throw new DomainException("inventory.reference", "The batch does not belong to this store and item.");
        if (input.Quantity is < 1 or > 100000) throw new DomainException("inventory.stock", "Consumption quantity must be between 1 and 100000.");
        Guard(batch, -input.Quantity);
        var department = CareGuard.Text(input.Department, 2, 60, "Department");
        var reason = CareGuard.Text(input.Reason, 3, 500, "Consumption reason");
        var number = await s.NextNumberAsync("CONS", $"{scope.BranchId}:{clock.UtcNow.Year}", $"CONS-{clock.UtcNow.Year}", ct);
        var consumption = new InventoryConsumption(Guid.NewGuid(), number, input.StoreId, input.ItemId, input.BatchId,
            input.Quantity, department, reason, scope.Actor.Id, clock.UtcNow);
        await s.InsertConsumptionAsync(consumption, ct);
        await s.UpdateInventoryBatchAsync(batch with { Quantity = batch.Quantity - input.Quantity, Version = batch.Version + 1 }, ct);
        await s.InsertInventoryMovementAsync(new InventoryMovement(Guid.NewGuid(), input.ItemId, input.StoreId, batch.Id,
            "Consumption", -input.Quantity, "Consumption", consumption.Id.ToString(), scope.Actor.Id, clock.UtcNow), ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "inventory.stock.consumed", "InventoryConsumption", consumption.Id, reason, new { input.Quantity, Department = department }), ct);
        return consumption;
    }, ct);

    public Task<IReadOnlyList<InventoryStore>> ListStores(Scope scope, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.InventoryRead); await CareGuard.Module(s, "inventory", ct);
        return await s.ListInventoryStoresAsync(ct);
    }, ct);
    public Task<IReadOnlyList<InventoryItem>> ListItems(Scope scope, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.InventoryRead); await CareGuard.Module(s, "inventory", ct);
        return await s.ListInventoryItemsAsync(ct);
    }, ct);
    public Task<IReadOnlyList<Vendor>> ListVendors(Scope scope, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.InventoryRead); await CareGuard.Module(s, "inventory", ct);
        return await s.ListVendorsAsync(ct);
    }, ct);
    public Task<IReadOnlyList<InventoryBatch>> ListBatches(Scope scope, Guid? storeId, Guid? itemId, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.InventoryRead); await CareGuard.Module(s, "inventory", ct);
        return await s.ListInventoryBatchesAsync(storeId, itemId, ct);
    }, ct);
    public Task<IReadOnlyList<InventoryIndent>> ListIndents(Scope scope, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.InventoryRead); await CareGuard.Module(s, "inventory", ct);
        return await s.ListInventoryIndentsAsync(ct);
    }, ct);
    public Task<IReadOnlyList<PurchaseOrder>> ListPurchaseOrders(Scope scope, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.InventoryRead); await CareGuard.Module(s, "inventory", ct);
        return await s.ListPurchaseOrdersAsync(ct);
    }, ct);
}

public sealed class CssdService(IUnitOfWork unitOfWork, IClock clock)
{
    public Task<InstrumentSet> RegisterSet(Scope scope, CssdSetInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.CssdProcess); await CareGuard.Module(s, "cssd", ct);
        var code = CareGuard.Text(input.Code, 2, 30, "Set code");
        var name = CareGuard.Text(input.Name, 2, 80, "Set name");
        if (input.Items is not { Count: > 0 and <= 40 }) throw new DomainException("cssd.completeness", "A set needs between 1 and 40 instruments.");
        var set = new InstrumentSet(Guid.NewGuid(), code, name, "Dirty", 0, null, null);
        await s.InsertInstrumentSetAsync(set, ct);
        foreach (var item in input.Items)
        {
            var itemName = CareGuard.Text(item.Name, 2, 60, "Instrument name");
            var serial = item.SerialNumber.Trim();
            await s.InsertInstrumentItemAsync(new InstrumentItem(Guid.NewGuid(), set.Id, itemName, serial, "Ok"), ct);
        }
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "cssd.set.registered", "InstrumentSet", set.Id, code, new { input.Items.Count }), ct);
        return set;
    }, ct);

    public Task<InstrumentSet> ReceiveReturn(Scope scope, Guid id, CssdReceiveInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.CssdProcess); await CareGuard.Module(s, "cssd", ct);
        var set = await s.FindInstrumentSetAsync(id, ct) ?? throw new DomainException("not_found", "Instrument set not found.", 404);
        if (set.Status != "Issued") throw new DomainException("cssd.state", "Only issued sets return to decontamination.", 409);
        if (!Phase4Rules.ReturnConditions.Contains(input.ReturnCondition)) throw new DomainException("cssd.completeness", "Return condition must be Complete, Incomplete or Damaged.");
        var note = CareGuard.Text(input.Note, 0, 500, "Return note");
        var missing = input.ReturnCondition == "Incomplete" ? set.MissingCount + 1 : set.MissingCount;
        var updated = set with { Status = "Dirty", MissingCount = missing, Version = set.Version + 1 };
        await s.UpdateInstrumentSetAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "cssd.set.returned", "InstrumentSet", id, note, new { input.ReturnCondition, Missing = missing }), ct);
        return updated;
    }, ct);

    public Task<InstrumentSet> CleanPack(Scope scope, Guid id, VersionInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.CssdProcess); await CareGuard.Module(s, "cssd", ct);
        var set = await s.FindInstrumentSetAsync(id, ct) ?? throw new DomainException("not_found", "Instrument set not found.", 404);
        CareGuard.Version(set.Version, input.Version);
        if (set.Status is not ("Dirty" or "Cleaning")) throw new DomainException("cssd.state", "Only dirty sets can be cleaned and packed.", 409);
        var updated = set with { Status = "Packed", Version = set.Version + 1 };
        await s.UpdateInstrumentSetAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "cssd.set.packed", "InstrumentSet", id, input.Reason), ct);
        return updated;
    }, ct);

    public Task<SterilizerCycle> StartCycle(Scope scope, CycleInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.CssdProcess); await CareGuard.Module(s, "cssd", ct);
        if (!Phase4Rules.CssdMethods.Contains(input.Method)) throw new DomainException("cssd.cycle", "Select a valid sterilization method.");
        var machine = CareGuard.Text(input.Machine, 2, 40, "Sterilizer machine");
        var tempOk = input.Method switch
        {
            "Steam" => input.TempC is >= 121 and <= 134,
            "Ethylene oxide" => input.TempC is >= 37 and <= 63,
            _ => input.TempC is >= 45 and <= 65
        };
        if (!tempOk || input.DurationMin is < 1 or > 240) throw new DomainException("cssd.cycle", "Cycle temperature or duration is outside the method range.");
        if (input.SetIds is not { Count: > 0 and <= 20 }) throw new DomainException("cssd.cycle", "Load between 1 and 20 sets per cycle.");
        var number = await s.NextNumberAsync("CYC", $"{scope.BranchId}:{clock.UtcNow.Year}", $"CYC-{clock.UtcNow.Year}", ct);
        var cycle = new SterilizerCycle(Guid.NewGuid(), number, input.Method, machine, input.TempC, input.DurationMin,
            "Running", clock.UtcNow, null, scope.Actor.Id);
        await s.InsertCycleAsync(cycle, ct);
        foreach (var setId in input.SetIds.Distinct())
        {
            var set = await s.FindInstrumentSetAsync(setId, ct) ?? throw new DomainException("not_found", "Loaded set not found.", 404);
            if (set.Status != "Packed") throw new DomainException("cssd.state", $"Set {set.Code} is not packed for sterilization.", 409);
            await s.InsertCycleLoadAsync(new CycleLoad(Guid.NewGuid(), cycle.Id, setId, "Sterilizing"), ct);
            await s.UpdateInstrumentSetAsync(set with { Status = "Sterilizing", LastCycleId = cycle.Id, Version = set.Version + 1 }, ct);
        }
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "cssd.cycle.started", "SterilizerCycle", cycle.Id, machine, new { input.Method, input.TempC, input.DurationMin, input.SetIds.Count }), ct);
        return cycle;
    }, ct);

    public Task<SterilizerCycle> RecordIndicator(Scope scope, Guid id, IndicatorInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.CssdProcess); await CareGuard.Module(s, "cssd", ct);
        var cycle = await s.FindCycleAsync(id, ct) ?? throw new DomainException("not_found", "Cycle not found.", 404);
        if (cycle.Status != "Running") throw new DomainException("cssd.state", "Indicators record on running cycles only.", 409);
        if (input.Kind is not ("Chemical" or "Biological")) throw new DomainException("cssd.cycle", "Indicator kind must be Chemical or Biological.");
        if (input.Result is not ("Pass" or "Fail")) throw new DomainException("cssd.cycle", "Indicator result must be Pass or Fail.");
        var note = CareGuard.Text(input.Note, 0, 500, "Indicator note");
        await s.InsertCycleIndicatorAsync(new CycleIndicator(Guid.NewGuid(), id, input.Kind, input.Result, note, scope.Actor.Id, clock.UtcNow), ct);
        if (input.Result == "Fail")
        {
            foreach (var load in await s.ListCycleLoadsAsync(id, ct))
            {
                var set = await s.FindInstrumentSetAsync(load.SetId, ct);
                if (set is not null) await s.UpdateInstrumentSetAsync(set with { Status = "Quarantined", Version = set.Version + 1 }, ct);
            }
            await s.UpdateCycleAsync(cycle with { Status = "Failed", EndedAt = clock.UtcNow, Version = cycle.Version + 1 }, ct);
            await s.AddAuditAsync(CareGuard.Event(scope, clock, "cssd.cycle.failed", "SterilizerCycle", id, note, new { input.Kind }), ct);
        }
        return await s.FindCycleAsync(id, ct) ?? cycle;
    }, ct);

    public Task<SterilizerCycle> ReleaseCycle(Scope scope, Guid id, CycleReleaseInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.CssdRelease); await CareGuard.Module(s, "cssd", ct);
        var cycle = await s.FindCycleAsync(id, ct) ?? throw new DomainException("not_found", "Cycle not found.", 404);
        CareGuard.Version(cycle.Version, input.Version);
        if (cycle.Status != "Running") throw new DomainException("cssd.state", "Only running cycles can be released.", 409);
        var indicators = await s.ListCycleIndicatorsAsync(id, ct);
        if (indicators.All(i => i.Kind != "Chemical") || indicators.Any(i => i.Result == "Fail")) throw new DomainException("cssd.release", "Release requires passing chemical indicators and no failed indicator.");
        var loads = await s.ListCycleLoadsAsync(id, ct);
        foreach (var load in loads)
        {
            var set = await s.FindInstrumentSetAsync(load.SetId, ct) ?? throw new DomainException("not_found", "Loaded set not found.", 404);
            if (set.MissingCount > 0) throw new DomainException("cssd.completeness", $"Set {set.Code} has missing instruments and cannot release.");
        }
        var expiry = clock.UtcNow.AddDays(cycle.Method == "Ethylene oxide" ? 180 : 30);
        foreach (var load in loads)
        {
            var set = (await s.FindInstrumentSetAsync(load.SetId, ct))!;
            await s.UpdateInstrumentSetAsync(set with { Status = "Sterile", SterileExpiry = expiry, Version = set.Version + 1 }, ct);
        }
        var updated = cycle with { Status = "Passed", EndedAt = clock.UtcNow, Version = cycle.Version + 1 };
        await s.UpdateCycleAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "cssd.cycle.released", "SterilizerCycle", id, input.Reason, new { Loads = loads.Count, Expiry = expiry }), ct);
        await s.AddOutboxAsync("cssd.cycle.released", id, JsonSerializer.Serialize(new { updated.Number, Loads = loads.Count }), ct);
        return updated;
    }, ct);

    public Task<CssdIssue> IssueSet(Scope scope, CssdIssueInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.CssdIssue); await CareGuard.Module(s, "cssd", ct);
        var set = await s.FindInstrumentSetAsync(input.SetId, ct) ?? throw new DomainException("not_found", "Instrument set not found.", 404);
        if (set.Status != "Sterile") throw new DomainException("cssd.state", "Only sterile sets can be issued.", 409);
        if (set.SterileExpiry is not { } expiry || expiry <= clock.UtcNow) throw new DomainException("cssd.state", "This set is past its sterile expiry.", 409);
        if (input.BookingId is Guid bookingId && await s.FindOtBookingAsync(bookingId, ct) is null) throw new DomainException("not_found", "Linked theatre booking not found.", 404);
        var issuedTo = CareGuard.Text(input.IssuedTo, 2, 80, "Issued to");
        var number = await s.NextNumberAsync("CSSD", $"{scope.BranchId}:{clock.UtcNow.Year}", $"CSSD-{clock.UtcNow.Year}", ct);
        var issue = new CssdIssue(Guid.NewGuid(), number, set.Id, input.BookingId, issuedTo, "Issued", "", clock.UtcNow, scope.Actor.Id, null);
        await s.InsertCssdIssueAsync(issue, ct);
        await s.UpdateInstrumentSetAsync(set with { Status = "Issued", Version = set.Version + 1 }, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "cssd.set.issued", "CssdIssue", issue.Id, issuedTo, new { input.BookingId }), ct);
        return issue;
    }, ct);

    public Task<CssdIssue> ReturnIssue(Scope scope, Guid id, CssdReturnInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.CssdIssue); await CareGuard.Module(s, "cssd", ct);
        var issue = await s.FindCssdIssueAsync(id, ct) ?? throw new DomainException("not_found", "CSSD issue not found.", 404);
        CareGuard.Version(issue.Version, input.Version);
        if (issue.Status != "Issued") throw new DomainException("cssd.state", "Only issued sets can be returned.", 409);
        if (!Phase4Rules.ReturnConditions.Contains(input.ReturnCondition)) throw new DomainException("cssd.completeness", "Return condition must be Complete, Incomplete or Damaged.");
        var set = await s.FindInstrumentSetAsync(issue.SetId, ct) ?? throw new DomainException("not_found", "Instrument set not found.", 404);
        var missing = input.ReturnCondition == "Incomplete" ? set.MissingCount + 1 : set.MissingCount;
        await s.UpdateInstrumentSetAsync(set with { Status = "Dirty", MissingCount = missing, Version = set.Version + 1 }, ct);
        var updated = issue with { Status = "Returned", ReturnCondition = input.ReturnCondition, ReturnedAt = clock.UtcNow, Version = issue.Version + 1 };
        await s.UpdateCssdIssueAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "cssd.issue.returned", "CssdIssue", id, input.ReturnCondition, new { input.ReturnCondition }), ct);
        return updated;
    }, ct);

    public Task<CssdRecall> Recall(Scope scope, RecallInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.CssdRecall); await CareGuard.Module(s, "cssd", ct);
        var cycle = await s.FindCycleAsync(input.CycleId, ct) ?? throw new DomainException("not_found", "Cycle not found.", 404);
        if (cycle.Status == "Running") throw new DomainException("cssd.recall", "Running cycles cannot be recalled before release.");
        if (await s.FindOpenRecallByCycleAsync(input.CycleId, ct) is not null) throw new DomainException("cssd.recall", "An open recall already exists for this cycle.", 409);
        var reason = CareGuard.Text(input.Reason, 5, 500, "Recall reason");
        var number = await s.NextNumberAsync("RCL", $"{scope.BranchId}:{clock.UtcNow.Year}", $"RCL-{clock.UtcNow.Year}", ct);
        var recall = new CssdRecall(Guid.NewGuid(), number, input.CycleId, reason, "Open", scope.Actor.Id, clock.UtcNow);
        await s.InsertCssdRecallAsync(recall, ct);
        foreach (var load in await s.ListCycleLoadsAsync(input.CycleId, ct))
            foreach (var issue in await s.ListCssdIssuesBySetAsync(load.SetId, ct))
                await s.AddAuditAsync(CareGuard.Event(scope, clock, "cssd.recall.affected", "CssdIssue", issue.Id, reason, new { recall.Number }), ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "cssd.recall.opened", "CssdRecall", recall.Id, reason, new { cycle.Number }), ct);
        await s.AddOutboxAsync("cssd.recall.opened", recall.Id, JsonSerializer.Serialize(new { recall.Number, Cycle = cycle.Number }), ct);
        return recall;
    }, ct);

    public Task<CssdRecall> CloseRecall(Scope scope, Guid id, VersionInput input, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.CssdRecall); await CareGuard.Module(s, "cssd", ct);
        var recall = await s.FindCssdRecallAsync(id, ct) ?? throw new DomainException("not_found", "Recall not found.", 404);
        if (recall.Status != "Open") throw new DomainException("cssd.state", "Only open recalls can be closed.", 409);
        var updated = recall with { Status = "Closed" };
        await s.UpdateCssdRecallAsync(updated, ct);
        await s.AddAuditAsync(CareGuard.Event(scope, clock, "cssd.recall.closed", "CssdRecall", id, input.Reason), ct);
        return updated;
    }, ct);

    public Task<IReadOnlyList<InstrumentSet>> ListSets(Scope scope, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.CssdRead); await CareGuard.Module(s, "cssd", ct);
        return await s.ListInstrumentSetsAsync(ct);
    }, ct);
    public Task<IReadOnlyList<SterilizerCycle>> ListCycles(Scope scope, CancellationToken ct) => unitOfWork.ExecuteAsync(scope, async s =>
    {
        scope.Require(Permissions.CssdRead); await CareGuard.Module(s, "cssd", ct);
        return await s.ListCyclesAsync(ct);
    }, ct);
}
