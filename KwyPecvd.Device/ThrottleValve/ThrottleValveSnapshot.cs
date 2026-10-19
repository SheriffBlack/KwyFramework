namespace KwyPecvd.Device.ThrottleValve;

/// <summary>
/// 节流阀某一时刻的运行状态快照。
/// </summary>
public sealed record ThrottleValveSnapshot
{
    public required string Id { get; init; }

    public required ThrottleValveMode CommandedMode { get; init; }

    public required ThrottleValveMode ActualMode { get; init; }

    public required double PositionSetPoint { get; init; }

    public required double PositionFeedback { get; init; }

    public required string PositionUnit { get; init; }

    public required double PressureSetPoint { get; init; }

    public required double PressureFeedback { get; init; }

    public required string PressureUnit { get; init; }

    public required bool IsOffline { get; init; }

    /// <summary>阀门位置及控制模式反馈是否可信。</summary>
    public required bool IsPositionFeedbackValid { get; init; }

    /// <summary>压力闭环使用的压力反馈是否可信。</summary>
    public required bool IsPressureFeedbackValid { get; init; }

    public required bool IsFaulted { get; init; }

    public required bool IsPositionOutOfTolerance { get; init; }

    public required bool IsPressureOutOfTolerance { get; init; }

    public string? DiagnosticCode { get; init; }

    public required DateTimeOffset Timestamp { get; init; }
}
