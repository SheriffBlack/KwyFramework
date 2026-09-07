using KwyTemplate.MES.Abstract.Models;

namespace KwyTemplate.MES.Abstract.Services;

public interface IMesWorkOrderService
{
    Task<MesResult<MesWorkOrderSetup>> GetWorkOrderSetupAsync(MesWorkOrderRequest request, CancellationToken cancellationToken = default);
}