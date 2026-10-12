namespace KwyPecvd.Device.PressureGauge;

/// <summary>
/// 压力计设备抽象。
/// 提供压力测量快照及周期采集能力。
/// </summary>
public interface IPressureGauge : IHardwareComponent
{
    PressureGaugeDefinition Definition { get; }

    PressureGaugeSnapshot Snapshot { get; }

    event EventHandler<PressureGaugeSnapshot>? SnapshotChanged;
}