using Careflow.Application;
using Careflow.Domain;
namespace Careflow.Api;

public static class CareEndpoints
{
    public static void Map(RouteGroupBuilder root)
    {
        Careflow.Application.Scope S(HttpContext c) => Careflow.Application.Scope.For((Principal)c.Items["principal"]!, c.RequestServices.GetRequiredService<IClock>().UtcNow);
        var ipd = root.MapGroup("/ipd");
        ipd.MapPost("/admissions", async (AdmissionInput input, HttpContext c, InpatientService s, CancellationToken ct) => Results.Ok(await s.Admit(S(c), input, ct)));
        ipd.MapPost("/admissions/{id:guid}/transfer", async (Guid id, TransferInput input, HttpContext c, InpatientService s, CancellationToken ct) => Results.Ok(await s.Transfer(S(c), id, input, ct)));
        ipd.MapPost("/admissions/{id:guid}/discharge", async (Guid id, DischargeInput input, HttpContext c, InpatientService s, CancellationToken ct) => Results.Ok(await s.Discharge(S(c), id, input, ct)));
        ipd.MapPut("/beds/{id:guid}/status", async (Guid id, BedStatusInput input, HttpContext c, InpatientService s, CancellationToken ct) => Results.Ok(await s.SetBedStatus(S(c), id, input, ct)));
        var nursing = root.MapGroup("/nursing");
        nursing.MapPost("/notes", async (NursingNoteInput input, HttpContext c, InpatientService s, CancellationToken ct) => Results.Ok(await s.AddNursingNote(S(c), input, ct)));
        nursing.MapPost("/medication-orders", async (MedicationOrderInput input, HttpContext c, InpatientService s, CancellationToken ct) => Results.Ok(await s.OrderMedication(S(c), input, ct)));
        nursing.MapPost("/medication-orders/{id:guid}/stop", async (Guid id, VersionInput input, HttpContext c, InpatientService s, CancellationToken ct) => Results.Ok(await s.StopMedication(S(c), id, input, ct)));
        nursing.MapPost("/administrations", async (AdministerInput input, HttpContext c, InpatientService s, CancellationToken ct) => Results.Ok(await s.Administer(S(c), input, ct)));
        nursing.MapPost("/handovers", async (HandoverInput input, HttpContext c, InpatientService s, CancellationToken ct) => Results.Ok(await s.CreateHandover(S(c), input, ct)));
        nursing.MapPost("/handovers/{id:guid}/acknowledge", async (Guid id, VersionInput input, HttpContext c, InpatientService s, CancellationToken ct) => Results.Ok(await s.AcknowledgeHandover(S(c), id, input, ct)));
        var pharmacy = root.MapGroup("/pharmacy");
        pharmacy.MapPost("/stock", async (ReceiveStockInput input, HttpContext c, PharmacyService s, CancellationToken ct) => Results.Ok(await s.ReceiveStock(S(c), input, ct)));
        pharmacy.MapPost("/dispenses", async (DispenseInput input, HttpContext c, PharmacyService s, CancellationToken ct) => Results.Ok(await s.Dispense(S(c), input, ct)));
        var lab = root.MapGroup("/lab");
        lab.MapPost("/orders", async (LabOrderInput input, HttpContext c, LaboratoryService s, CancellationToken ct) => Results.Ok(await s.Order(S(c), input, ct)));
        lab.MapPost("/orders/{id:guid}/collect", async (Guid id, CollectSpecimenInput input, HttpContext c, LaboratoryService s, CancellationToken ct) => Results.Ok(await s.Collect(S(c), id, input, ct)));
        lab.MapPost("/orders/{id:guid}/reject", async (Guid id, VersionInput input, HttpContext c, LaboratoryService s, CancellationToken ct) => Results.Ok(await s.Reject(S(c), id, input, ct)));
        lab.MapPost("/orders/{id:guid}/process", async (Guid id, VersionInput input, HttpContext c, LaboratoryService s, CancellationToken ct) => Results.Ok(await s.Process(S(c), id, input, ct)));
        lab.MapPost("/orders/{id:guid}/results", async (Guid id, LabResultInput input, HttpContext c, LaboratoryService s, CancellationToken ct) => Results.Ok(await s.EnterResult(S(c), id, input, ct)));
        lab.MapPost("/orders/{id:guid}/validate", async (Guid id, VersionInput input, HttpContext c, LaboratoryService s, CancellationToken ct) => Results.Ok(await s.Validate(S(c), id, input, ct)));
        lab.MapPost("/orders/{id:guid}/authorize", async (Guid id, VersionInput input, HttpContext c, LaboratoryService s, CancellationToken ct) => Results.Ok(await s.Authorize(S(c), id, input, ct)));
        root.MapPost("/alerts/{id:guid}/acknowledge", async (Guid id, AlertAcknowledgeInput input, HttpContext c, LaboratoryService s, CancellationToken ct) => Results.Ok(await s.AcknowledgeAlert(S(c), id, input, ct)));
    }
}
