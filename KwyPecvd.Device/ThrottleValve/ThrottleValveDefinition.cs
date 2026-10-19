namespace KwyPecvd.Device.ThrottleValve;

/// <summary>
/// 节流阀静态设备定义。
/// </summary>
public sealed record ThrottleValveDefinition
{
    public required string Id { get; init; }

    public required string ModuleId { get; init; }

    public required string DisplayName { get; init; }

    public string PositionUnit { get; init; } = "%";

    public double MinimumPosition { get; init; } = 0;

    public double MaximumPosition { get; init; } = 100;

    public string PressureUnit { get; init; } = "Torr";

    public required double MaximumPressure { get; init; }

    /// <summary>
    /// 通信异常或停机时要求的安全模式。
    /// 只能配置为全开或全关。
    /// </summary>
    public ThrottleValveMode SafeMode { get; init; } = ThrottleValveMode.Closed;

    /// <summary>位置反馈允许误差。</summary>
    public double PositionTolerance { get; init; } = 1;

    /// <summary>压力反馈允许误差。</summary>
    public double PressureTolerance { get; init; } = 0.05;

    /// <summary>持续偏离目标多久后才报告超差。</summary>
    public TimeSpan ToleranceDelay { get; init; } = TimeSpan.FromSeconds(3);

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(ModuleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(DisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(PositionUnit);
        ArgumentException.ThrowIfNullOrWhiteSpace(PressureUnit);

        if (!double.IsFinite(MinimumPosition))
        {
            throw new ArgumentOutOfRangeException(
                nameof(MinimumPosition));
        }

        if (!double.IsFinite(MaximumPosition) ||
            MaximumPosition <= MinimumPosition)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumPosition));
        }

        if (!double.IsFinite(MaximumPressure) ||
            MaximumPressure <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumPressure));
        }

        if (SafeMode is not (
            ThrottleValveMode.Open or
            ThrottleValveMode.Closed))
        {
            throw new ArgumentOutOfRangeException(
                nameof(SafeMode),
                "Safe mode must be Open or Closed.");
        }

        if (!double.IsFinite(PositionTolerance) ||
            PositionTolerance < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(PositionTolerance));
        }

        if (!double.IsFinite(PressureTolerance) ||
            PressureTolerance < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(PressureTolerance));
        }

        if (ToleranceDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ToleranceDelay));
        }
    }
}