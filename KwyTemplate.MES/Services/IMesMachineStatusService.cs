using KwyTemplate.MES.Models;

namespace KwyTemplate.MES.Services;

public interface IMesMachineStatusService
{
    Task<MesResult> UploadMachineStatusAsync(MesMachineStatusUploadRequest request, CancellationToken cancellationToken = default);
}