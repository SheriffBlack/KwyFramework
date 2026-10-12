namespace KwyPecvd.Device.Thermal.TemperatureController;

/// <summary>
/// 温度控制器的静态设备定义。
/// </summary>
public sealed record TemperatureControllerDefinition
{
    public required string Id { get; init; }

    public required string ModuleId { get; init; }

    public required string DisplayName { get; init; }

    public required string Unit { get; init; }

    public required double MinimumSetPoint { get; init; }

    public required double MaximumSetPoint { get; init; }

    /// <summary>温度到达判定允许偏差。</summary>
    public required double Tolerance { get; init; }

    /// <summary>超差持续多久才报告诊断。</summary>
    public TimeSpan ToleranceDelay { get; init; } =
        TimeSpan.FromSeconds(5);

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(ModuleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(DisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(Unit);

        if (!double.IsFinite(MinimumSetPoint))
        {
            throw new ArgumentOutOfRangeException(
                nameof(MinimumSetPoint));
        }

        if (!double.IsFinite(MaximumSetPoint) ||
            MaximumSetPoint <= MinimumSetPoint)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumSetPoint));
        }

        if (!double.IsFinite(Tolerance) ||
            Tolerance < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Tolerance));
        }

        if (ToleranceDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ToleranceDelay));
        }
    }
}