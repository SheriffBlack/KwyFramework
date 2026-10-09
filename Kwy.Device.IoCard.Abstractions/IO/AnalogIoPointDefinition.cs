namespace Kwy.Device.IoCard.Abstractions;

/// <summary>模拟量 IO 点位的信号方向。</summary>
public enum AnalogIoDirection
{
    Input,
    Output
}

/// <summary>模拟量通道的电气信号类型。</summary>
public enum AnalogElectricalSignal
{
    Raw,
    Voltage,
    Current,
    Resistance,
    Thermocouple,
    Rtd
}

/// <summary>模拟量样本质量。断线等硬件状态由设备适配器上报。</summary>
public enum AnalogIoQuality
{
    Good,
    UnderRange,
    OverRange,
    OpenCircuit,
    DeviceUnavailable,
    Invalid
}

/// <summary>含原始量、工程量、时间戳和质量的模拟量样本。</summary>
public readonly record struct AnalogIoSample(
    string PointId,
    double Value,
    double RawValue,
    DateTimeOffset Timestamp,
    AnalogIoQuality Quality);

/// <summary>
/// 模拟量 IO 点位的稳定业务身份、物理通道及线性工程量换算。
/// </summary>
public sealed record AnalogIoPointDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string DeviceId { get; init; }
    public required AnalogIoDirection Direction { get; init; }
    public int Channel { get; init; }
    public AnalogElectricalSignal ElectricalSignal { get; init; }

    /// <summary>设备适配器返回的有效原始量下限，例如 4 mA 或 0 V。</summary>
    public double RawMinimum { get; init; }
    /// <summary>设备适配器返回的有效原始量上限，例如 20 mA 或 10 V。</summary>
    public double RawMaximum { get; init; } = 1;
    public double EngineeringMinimum { get; init; }
    public double EngineeringMaximum { get; init; } = 1;
    public string? Unit { get; init; }
    public double? ProcessSafeValue { get; init; }
    public double? MinimumAllowedValue { get; init; }
    public double? MaximumAllowedValue { get; init; }
    public IoPointCriticality Criticality { get; init; }
    public string? Group { get; init; }
    public string? Owner { get; init; }
    public string? Description { get; init; }

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(DeviceId);
        if (!Enum.IsDefined(Direction)) throw new ArgumentOutOfRangeException(nameof(Direction));
        if (!Enum.IsDefined(ElectricalSignal)) throw new ArgumentOutOfRangeException(nameof(ElectricalSignal));
        if (!Enum.IsDefined(Criticality)) throw new ArgumentOutOfRangeException(nameof(Criticality));
        if (Channel is < 0 or >= AnalogIoChannelLimits.DefaultChannelCount)
            throw new ArgumentOutOfRangeException(nameof(Channel), Channel, "模拟量通道必须位于 0～63。");
        ValidateRange(RawMinimum, RawMaximum, nameof(RawMinimum), nameof(RawMaximum));
        ValidateRange(EngineeringMinimum, EngineeringMaximum, nameof(EngineeringMinimum), nameof(EngineeringMaximum));

        if (Direction == AnalogIoDirection.Input && (ProcessSafeValue is not null || Owner is not null))
            throw new InvalidOperationException($"模拟输入点位“{Id}”不能配置安全输出值或 Owner。");

        ValidateEngineeringValue(ProcessSafeValue, nameof(ProcessSafeValue));
        ValidateEngineeringValue(MinimumAllowedValue, nameof(MinimumAllowedValue));
        ValidateEngineeringValue(MaximumAllowedValue, nameof(MaximumAllowedValue));
        if (MinimumAllowedValue is { } min && MaximumAllowedValue is { } max && min > max)
            throw new ArgumentException("MinimumAllowedValue 不能大于 MaximumAllowedValue。");
    }

    public double ToEngineeringValue(double rawValue)
    {
        if (!double.IsFinite(rawValue)) throw new ArgumentOutOfRangeException(nameof(rawValue));
        return (rawValue - RawMinimum) / (RawMaximum - RawMinimum)
            * (EngineeringMaximum - EngineeringMinimum) + EngineeringMinimum;
    }

    public double ToRawValue(double engineeringValue)
    {
        if (!double.IsFinite(engineeringValue)) throw new ArgumentOutOfRangeException(nameof(engineeringValue));
        return (engineeringValue - EngineeringMinimum) / (EngineeringMaximum - EngineeringMinimum)
            * (RawMaximum - RawMinimum) + RawMinimum;
    }

    private void ValidateEngineeringValue(double? value, string parameterName)
    {
        if (value is not { } actual) return;
        if (!double.IsFinite(actual) || actual < EngineeringMinimum || actual > EngineeringMaximum)
            throw new ArgumentOutOfRangeException(parameterName, actual, $"必须位于工程量程 [{EngineeringMinimum}, {EngineeringMaximum}] 内。");
    }

    private static void ValidateRange(double minimum, double maximum, string minimumName, string maximumName)
    {
        if (!double.IsFinite(minimum)) throw new ArgumentOutOfRangeException(minimumName);
        if (!double.IsFinite(maximum) || maximum <= minimum) throw new ArgumentOutOfRangeException(maximumName);
    }
}
