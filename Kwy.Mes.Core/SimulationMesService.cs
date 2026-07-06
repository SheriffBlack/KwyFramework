using Kwy.Mes.Abstractions.Models;

namespace Kwy.Mes.Core;

/// <summary>
/// Lightweight MES implementation for templates, demos and offline commissioning.
/// </summary>
public sealed class SimulationMesService : MesServiceBase
{
    public override Task<MesResult<MesWorkOrder>> GetWorkOrderAsync(string workOrderNo, CancellationToken cancellationToken = default)
    {
        var order = new MesWorkOrder(workOrderNo, ProductCode: "SIM-PRODUCT", PlannedQuantity: 1);
        return Task.FromResult(MesResult<MesWorkOrder>.Ok(order));
    }

    public override Task<MesResult<MesRouteCheckResult>> CheckRouteAsync(MesUnit unit, MesStation station, CancellationToken cancellationToken = default)
    {
        var result = new MesRouteCheckResult(Allowed: true, Code: "OK", Message: "Route allowed.");
        return Task.FromResult(MesResult<MesRouteCheckResult>.Ok(result));
    }

    public override Task<MesResult<MesRecipe>> GetRecipeAsync(string recipeName, CancellationToken cancellationToken = default)
        => Task.FromResult(MesResult<MesRecipe>.Ok(MesRecipe.Empty(recipeName)));

    public override Task<MesResult> UploadTestResultAsync(MesTestResult result, CancellationToken cancellationToken = default)
        => Task.FromResult(MesResult.Ok());

    public override Task<MesResult> UploadTraceAsync(MesTraceRecord record, CancellationToken cancellationToken = default)
        => Task.FromResult(MesResult.Ok());
}
