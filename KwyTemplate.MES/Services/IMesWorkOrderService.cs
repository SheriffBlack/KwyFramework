using KwyTemplate.MES.Models;

namespace KwyTemplate.MES.Services;

public interface IMesWorkOrderService
{
    Task<MesResult<MesWorkOrderSetup>> GetWorkOrderSetupAsync(MesWorkOrderRequest request, CancellationToken cancellationToken = default);
}