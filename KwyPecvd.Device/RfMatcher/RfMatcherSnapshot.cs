namespace KwyPecvd.Device.RfMatcher;

/// <summary>
/// RF匹配器在某一时刻的状态。
/// </summary>
public sealed record RfMatcherSnapshot
{
    public required string Id { get; init; }

    /// <summary>
    /// 软件要求的工作模式。
    /// </summary>
    public required RfMatcherMode CommandedMode { get; init; }

    /// <summary>
    /// 设备反馈的实际工作模式。
    /// </summary>
    public required RfMatcherMode ActualMode { get; init; }

    public required double TuneSetPoint { get; init; }

    public required double TunePosition { get; init; }

    public required double LoadSetPoint { get; init; }

    public required double LoadPosition { get; init; }

    public required string PositionUnit { get; init; }

    /// <summary>
    /// 匹配器是否正在自动调谐。
    /// </summary>
    public required bool IsTuning { get; init; }

    /// <summary>
    /// 匹配器是否已达到匹配状态。
    /// </summary>
    public required bool IsMatched { get; init; }

    public required bool IsOffline { get; init; }

    public required bool IsFeedbackValid { get; init; }

    public required bool IsFaulted { get; init; }

    public required bool IsPositionOutOfTolerance { get; init; }

    public required string? DiagnosticCode { get; init; }

    public required DateTimeOffset Timestamp { get; init; }
}