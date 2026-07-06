using Kwy.Mes.Abstractions.Models;

namespace Kwy.Mes.Abstractions;

public interface IMesWorkOrderService
{
    Task<MesResult<MesWorkOrder>> GetWorkOrderAsync(string workOrderNo, CancellationToken cancellationToken = default);
}
