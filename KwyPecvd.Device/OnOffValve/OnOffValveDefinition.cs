namespace KwyPecvd.Device.OnOffValve;

public sealed record OnOffValveDefinition
{
    public required string Id { get; init; }

    public required string ModuleId { get; init; }

    public required string DisplayName { get; init; }

    /// <summary>关闭或撤销控制后的工艺安全位置。</summary>
    public OnOffValvePosition SafePosition { get; init; } =
        OnOffValvePosition.Closed;

    /// <summary>阀门动作允许的最长时间。</summary>
    public TimeSpan MotionTimeout { get; init; } =
        TimeSpan.FromSeconds(10);

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(ModuleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(DisplayName);

        if (SafePosition is not
            OnOffValvePosition.Open and
            not OnOffValvePosition.Closed)
        {
            throw new ArgumentOutOfRangeException(
                nameof(SafePosition),
                SafePosition,
                "Valve safe position must be Open or Closed.");
        }

        if (MotionTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MotionTimeout));
        }
    }
}
