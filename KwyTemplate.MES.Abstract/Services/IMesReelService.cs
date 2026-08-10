using KwyTemplate.MES.Abstract.Models;

namespace KwyTemplate.MES.Abstract.Services;

public interface IMesReelService
{
    Task<MesResult<MesReelScanResult>> ScanReelAsync(MesReelScanRequest request, CancellationToken cancellationToken = default);
}