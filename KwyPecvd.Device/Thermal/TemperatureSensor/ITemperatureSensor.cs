namespace KwyPecvd.Device.Thermal.TemperatureSensor;

/// <summary>
/// 温度传感器设备抽象。
/// 提供温度测量快照及周期采集能力。
/// </summary>
public interface ITemperatureSensor : IHardwareComponent
{
    TemperatureSensorDefinition Definition { get; }

    TemperatureSensorSnapshot Snapshot { get; }

    event EventHandler<TemperatureSensorSnapshot>? SnapshotChanged;
}