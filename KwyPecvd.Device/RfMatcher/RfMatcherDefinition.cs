namespace KwyPecvd.Device.RfMatcher;

/// <summary>
/// RF匹配器静态设备定义。
/// </summary>
public sealed record RfMatcherDefinition
{
    public required string Id { get; init; }

    public required string ModuleId { get; init; }

    public required string DisplayName { get; init; }

    public string PositionUnit { get; init; } = "%";

    public double MinimumPosition { get; init; } = 0;

    public double MaximumPosition { get; init; } = 100;

    /// <summary>
    /// 手动位置反馈允许偏差。
    /// </summary>
    public double PositionTolerance { get; init; } = 1;

    /// <summary>
    /// 手动位置持续偏离多久后报告异常。
    /// </summary>
    public TimeSpan PositionToleranceDelay { get; init; } = TimeSpan.FromSeconds(3);

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Id);

        ArgumentException.ThrowIfNullOrWhiteSpace(ModuleId);

        ArgumentException.ThrowIfNullOrWhiteSpace(DisplayName);

        ArgumentException.ThrowIfNullOrWhiteSpace(PositionUnit);

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

        if (!double.IsFinite(
                PositionTolerance) ||
            PositionTolerance < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(PositionTolerance));
        }

        if (PositionToleranceDelay <
            TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(PositionToleranceDelay));
        }
    }
}