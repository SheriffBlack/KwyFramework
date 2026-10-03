using KwyTemplate.MES.Models;

namespace KwyTemplate.MES.Services;

public interface IMesReelService
{
    Task<MesResult<MesReelScanResult>> ScanReelAsync(MesReelScanRequest request, CancellationToken cancellationToken = default);
}