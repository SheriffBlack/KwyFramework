using KwyPecvd.Device.OnOffValve;
using KwyPecvd.Device.PressureGauge;
using KwyPecvd.Device.VacuumPump;

namespace KwyPecvd.Process.Chamber.Vacuum;

/// <summary>
/// 腔体真空子系统的聚合快照。
/// </summary>
/// <remarks>
/// State / FaultCode -- 真空工艺子系统的判断
///
/// PressureGauges / Pumps / Valves -- 各硬件组件的原始状态
/// </remarks>
public sealed record ChamberVacuumSnapshot
{
    public required ChamberVacuumState State { get; init; }

    public required IReadOnlyCollection<PressureGaugeSnapshot> PressureGauges { get; init; }

    public required IReadOnlyCollection<VacuumPumpSnapshot> Pumps { get; init; }

    public required IReadOnlyCollection<OnOffValveSnapshot> Valves { get; init; }

    public required DateTimeOffset StateChangedAt { get; init; }

    public required string? FaultCode { get; init; }

    public required string? FaultMessage { get; init; }

    public required DateTimeOffset Timestamp { get; init; }
}
