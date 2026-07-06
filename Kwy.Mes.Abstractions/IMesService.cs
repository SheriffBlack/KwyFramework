namespace Kwy.Mes.Abstractions;

/// <summary>
/// Standard MES facade for equipment business code.
/// </summary>
public interface IMesService :
    IMesConnection,
    IMesWorkOrderService,
    IMesRouteService,
    IMesRecipeService,
    IMesResultService,
    IMesTraceService
{
}
