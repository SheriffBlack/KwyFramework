namespace KwyPecvd.Device.Thermal.TemperatureSensor;

/// <summary>
/// 温度传感器的静态设备定义。
/// </summary>
public sealed record TemperatureSensorDefinition
{
    public required string Id { get; init; }

    public required string ModuleId { get; init; }

    public required string DisplayName { get; init; }

    public required string Unit { get; init; }

    /// <summary>传感器有效量程下限。</summary>
    public required double Minimum { get; init; }

    /// <summary>传感器有效量程上限。</summary>
    public required double Maximum { get; init; }

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(ModuleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(DisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(Unit);

        if (!double.IsFinite(Minimum))
        {
            throw new ArgumentOutOfRangeException(
                nameof(Minimum));
        }

        if (!double.IsFinite(Maximum) ||
            Maximum <= Minimum)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Maximum));
        }
    }
}