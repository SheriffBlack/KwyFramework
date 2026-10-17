namespace KwyPecvd.Process.Chamber.Thermal;

/// <summary>
/// 腔体中一个独立温区的静态硬件绑定。
/// </summary>
public sealed record ChamberThermalZoneDefinition
{
    /// <summary>
    /// 稳定的业务温区 ID，例如 UpperElectrode。
    /// </summary>
    public required string ZoneId { get; init; }

    public required string DisplayName { get; init; }

    /// <summary>
    /// 负责该温区闭环控制的温控器 ID。
    /// </summary>
    public required string ControllerId { get; init; }

    /// <summary>
    /// 该温区附加的独立监测传感器。
    /// 温控器自身的 Feedback 仍是主要控制反馈。
    /// </summary>
    public IReadOnlyCollection<string> MonitoringSensorIds { get; init; } = Array.Empty<string>();

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ZoneId);

        ArgumentException.ThrowIfNullOrWhiteSpace(DisplayName);

        ArgumentException.ThrowIfNullOrWhiteSpace(ControllerId);

        foreach (var sensorId in MonitoringSensorIds)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sensorId);
        }

        var duplicateSensor =
            MonitoringSensorIds
                .GroupBy(
                    id => id,
                    StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(
                    group => group.Count() > 1);

        if (duplicateSensor is not null)
        {
            throw new ArgumentException(
                $"Thermal zone '{ZoneId}' contains " +
                $"duplicate monitoring sensor " +
                $"'{duplicateSensor.Key}'.",
                nameof(MonitoringSensorIds));
        }
    }
}