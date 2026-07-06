using Kwy.Mes.Abstractions.Models;

namespace Kwy.Mes.Abstractions;

public interface IMesRouteService
{
    Task<MesResult<MesRouteCheckResult>> CheckRouteAsync(MesUnit unit, MesStation station, CancellationToken cancellationToken = default);
}
