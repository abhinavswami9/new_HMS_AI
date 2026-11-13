using Careflow.Application;
using Careflow.Domain;
namespace Careflow.Api;

public static class Phase3Endpoints
{
    public static void Map(RouteGroupBuilder root)
    {
        Careflow.Application.Scope S(HttpContext c) => Careflow.Application.Scope.For((Principal)c.Items["principal"]!, c.RequestServices.GetRequiredService<IClock>().UtcNow);
        var radiology = root.MapGroup("/radiology");
        radiology.MapPost("/orders", async (RadiologyOrderInput input, HttpContext c, RadiologyService s, CancellationToken ct) => Results.Ok(await s.Order(S(c), input, ct)));
        radiology.MapPost("/orders/{id:guid}/schedule", async (Guid id, RadiologyScheduleInput input, HttpContext c, RadiologyService s, CancellationToken ct) => Results.Ok(await s.Schedule(S(c), id, input, ct)));
        radiology.MapPost("/orders/{id:guid}/perform", async (Guid id, RadiologyPerformInput input, HttpContext c, RadiologyService s, CancellationToken ct) => Results.Ok(await s.Perform(S(c), id, input, ct)));
        radiology.MapPost("/orders/{id:guid}/report", async (Guid id, RadiologyReportInput input, HttpContext c, RadiologyService s, CancellationToken ct) => Results.Ok(await s.Report(S(c), id, input, ct)));
        radiology.MapPost("/orders/{id:guid}/authorize", async (Guid id, VersionInput input, HttpContext c, RadiologyService s, CancellationToken ct) => Results.Ok(await s.Authorize(S(c), id, input, ct)));
        radiology.MapPost("/orders/{id:guid}/cancel", async (Guid id, VersionInput input, HttpContext c, RadiologyService s, CancellationToken ct) => Results.Ok(await s.Cancel(S(c), id, input, ct)));
        // PACS boundary (docs/MODULE_SPECIFICATIONS.md §15): no live DICOM archive or viewer
        // is connected. The launch endpoint fails closed and loudly instead of fabricating one.
        radiology.MapGet("/studies/{id:guid}/viewer-launch", (Guid id) => Results.Json(
            new { title = "PACS/DICOM archive integration is not connected in this deployment. Configure an approved archive and viewer broker before enabling the pacs module.", code = "integration.unavailable" },
            statusCode: 501));
        var emergency = root.MapGroup("/emergency");
        emergency.MapPost("/registrations", async (EmergencyRegistrationInput input, HttpContext c, EmergencyService s, CancellationToken ct) => Results.Ok(await s.Register(S(c), input, ct)));
        emergency.MapPost("/registrations/{id:guid}/triage", async (Guid id, TriageInput input, HttpContext c, EmergencyService s, CancellationToken ct) => Results.Ok(await s.Triage(S(c), id, input, ct)));
        emergency.MapPost("/registrations/{id:guid}/observations", async (Guid id, EmergencyObservationInput input, HttpContext c, EmergencyService s, CancellationToken ct) => Results.Ok(await s.Observe(S(c), id, input, ct)));
        emergency.MapPost("/registrations/{id:guid}/disposition", async (Guid id, DispositionInput input, HttpContext c, EmergencyService s, CancellationToken ct) => Results.Ok(await s.Dispose(S(c), id, input, ct)));
        var ot = root.MapGroup("/ot");
        ot.MapPost("/bookings", async (OtBookingInput input, HttpContext c, OtService s, CancellationToken ct) => Results.Ok(await s.Request(S(c), input, ct)));
        ot.MapPost("/bookings/{id:guid}/schedule", async (Guid id, OtScheduleInput input, HttpContext c, OtService s, CancellationToken ct) => Results.Ok(await s.Schedule(S(c), id, input, ct)));
        ot.MapPost("/bookings/{id:guid}/checklist", async (Guid id, OtChecklistInput input, HttpContext c, OtService s, CancellationToken ct) => Results.Ok(await s.Checklist(S(c), id, input, ct)));
        ot.MapPost("/bookings/{id:guid}/time-in", async (Guid id, VersionInput input, HttpContext c, OtService s, CancellationToken ct) => Results.Ok(await s.TimeIn(S(c), id, input, ct)));
        ot.MapPost("/bookings/{id:guid}/time-out", async (Guid id, VersionInput input, HttpContext c, OtService s, CancellationToken ct) => Results.Ok(await s.TimeOut(S(c), id, input, ct)));
        ot.MapPost("/bookings/{id:guid}/complete", async (Guid id, OtCompleteInput input, HttpContext c, OtService s, CancellationToken ct) => Results.Ok(await s.Complete(S(c), id, input, ct)));
        ot.MapPost("/bookings/{id:guid}/cancel", async (Guid id, VersionInput input, HttpContext c, OtService s, CancellationToken ct) => Results.Ok(await s.Cancel(S(c), id, input, ct)));
        var blood = root.MapGroup("/blood");
        blood.MapPost("/units", async (ReceiveUnitInput input, HttpContext c, BloodBankService s, CancellationToken ct) => Results.Ok(await s.ReceiveUnit(S(c), input, ct)));
        blood.MapPost("/units/{id:guid}/screen", async (Guid id, ScreeningInput input, HttpContext c, BloodBankService s, CancellationToken ct) => Results.Ok(await s.Screen(S(c), id, input, ct)));
        blood.MapPost("/units/{id:guid}/dispose", async (Guid id, VersionInput input, HttpContext c, BloodBankService s, CancellationToken ct) => Results.Ok(await s.Dispose(S(c), id, input, ct)));
        blood.MapPost("/crossmatches", async (CrossmatchInput input, HttpContext c, BloodBankService s, CancellationToken ct) => Results.Ok(await s.Crossmatch(S(c), input, ct)));
        blood.MapPost("/issues", async (BloodIssueInput input, HttpContext c, BloodBankService s, CancellationToken ct) => Results.Ok(await s.Issue(S(c), input, ct)));
        blood.MapPost("/issues/{id:guid}/transfuse", async (Guid id, TransfuseInput input, HttpContext c, BloodBankService s, CancellationToken ct) => Results.Ok(await s.Transfuse(S(c), id, input, ct)));
        blood.MapPost("/issues/{id:guid}/return", async (Guid id, ReturnInput input, HttpContext c, BloodBankService s, CancellationToken ct) => Results.Ok(await s.Return(S(c), id, input, ct)));
    }
}
