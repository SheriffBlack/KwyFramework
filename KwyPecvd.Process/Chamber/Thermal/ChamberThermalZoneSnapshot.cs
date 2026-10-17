using KwyPecvd.Device.Thermal.TemperatureController;
using KwyPecvd.Device.Thermal.TemperatureSensor;

namespace KwyPecvd.Process.Chamber.Thermal;

/// <summary>
/// 腔体中一个温区的当前不可变运行快照。
/// </summary>
public sealed record ChamberThermalZoneSnapshot
{
    public required string ZoneId { get; init; }

    public required string DisplayName { get; init; }

    /// <summary>
    /// 本次温度准备要求的目标温度。
    /// 没有活动目标时为 null。
    /// </summary>
    public required double? TargetTemperature { get; init; }

    /// <summary>
    /// 控制器实际温度是否已进入目标允许范围。
    /// 没有活动目标时为 false。
    /// </summary>
    public required bool IsWithinTolerance { get; init; }

    /// <summary>
    /// 当前温区是否已经满足温度准备要求。
    /// </summary>
    public required bool IsReady { get; init; }

    /// <summary>
    /// 负责该温区控制的温控器快照。
    /// </summary>
    public required TemperatureControllerSnapshot Controller { get; init; }

    /// <summary>
    /// 该温区独立监测传感器的当前快照。
    /// </summary>
    public required IReadOnlyCollection<TemperatureSensorSnapshot> MonitoringSensors { get; init; }
}