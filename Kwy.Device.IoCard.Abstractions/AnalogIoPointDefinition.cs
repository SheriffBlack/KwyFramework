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

    /// <summary>原始信号与工程量之间的线性量程定义。</summary>
    public required AnalogIoScale Scale { get; init; }
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
        Scale.Validate();

        if (Direction == AnalogIoDirection.Input && (ProcessSafeValue is not null || Owner is not null))
            throw new InvalidOperationException($"模拟输入点位“{Id}”不能配置安全输出值或 Owner。");

        ValidateEngineeringValue(ProcessSafeValue, nameof(ProcessSafeValue));
        ValidateEngineeringValue(MinimumAllowedValue, nameof(MinimumAllowedValue));
        ValidateEngineeringValue(MaximumAllowedValue, nameof(MaximumAllowedValue));
        if (MinimumAllowedValue is { } min && MaximumAllowedValue is { } max && min > max)
            throw new ArgumentException("MinimumAllowedValue 不能大于 MaximumAllowedValue。");
    }

    private void ValidateEngineeringValue(double? value, string parameterName)
    {
        if (value is not { } actual) return;
        if (!double.IsFinite(actual) || actual < Scale.EngineeringMinimum || actual > Scale.EngineeringMaximum)
            throw new ArgumentOutOfRangeException(parameterName, actual, $"必须位于工程量程 [{Scale.EngineeringMinimum}, {Scale.EngineeringMaximum}] 内。");
    }
}
