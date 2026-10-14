using KwyPecvd.Device.PressureGauge;
using KwyPecvd.Device.VacuumPump;

namespace KwyPecvd.Process.Chamber.Vacuum;

/// <summary>
/// 腔体真空子系统的聚合快照。
/// </summary>
public sealed record ChamberVacuumSnapshot
{
    public required PressureGaugeSnapshot PressureGauge { get; init; }

    public required VacuumPumpSnapshot DryPump { get; init; }
}