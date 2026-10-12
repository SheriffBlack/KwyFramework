namespace KwyPecvd.Device.RfPowerSupply;

/// <summary>
/// RF电源的静态设备定义。
/// </summary>
public sealed record RfPowerSupplyDefinition
{
    public required string Id { get; init; }

    public required string ModuleId { get; init; }

    public required string DisplayName { get; init; }

    public string Unit { get; init; } = "W";

    /// <summary>RF额定频率，例如13.56 MHz。</summary>
    public required double FrequencyHz { get; init; }

    /// <summary>最大允许设定功率。</summary>
    public required double MaximumPower { get; init; }

    /// <summary>反射功率报警阈值。</summary>
    public required double ReflectedPowerLimit { get; init; }

    /// <summary>正向功率到达判定允许偏差。</summary>
    public required double Tolerance { get; init; }

    /// <summary>反射功率持续超过阈值多久后才报告异常。</summary>
    public TimeSpan ReflectedPowerDelay { get; init; } = TimeSpan.FromSeconds(1);

    public TimeSpan ToleranceDelay { get; init; } = TimeSpan.FromSeconds(3);

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(ModuleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            DisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(Unit);

        if (!double.IsFinite(FrequencyHz) ||
            FrequencyHz <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(FrequencyHz));
        }

        if (!double.IsFinite(MaximumPower) ||
            MaximumPower <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumPower));
        }

        if (!double.IsFinite(ReflectedPowerLimit) ||
            ReflectedPowerLimit < 0 ||
            ReflectedPowerLimit > MaximumPower)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ReflectedPowerLimit));
        }

        if (!double.IsFinite(Tolerance) ||
            Tolerance < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Tolerance));
        }

        if (ReflectedPowerDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ReflectedPowerDelay));
        }

        if (ToleranceDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ToleranceDelay));
        }
    }
}