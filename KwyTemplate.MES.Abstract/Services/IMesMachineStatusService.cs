using KwyTemplate.MES.Abstract.Models;

namespace KwyTemplate.MES.Abstract.Services;

public interface IMesMachineStatusService
{
    Task<MesResult> UploadMachineStatusAsync(MesMachineStatusUploadRequest request, CancellationToken cancellationToken = default);
}