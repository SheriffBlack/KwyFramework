namespace KwyPecvd.Device.Mfc;

/// <summary>某一时刻的 MFC 不可变运行快照。</summary>
public sealed record MfcSnapshot
{
    public required string Id { get; init; }

    /// <summary>当前下发给 MFC 的流量设定值。</summary>
    public required double SetPoint { get; init; }

    /// <summary>MFC 反馈的实际流量。</summary>
    public required double Feedback { get; init; }

    /// <summary>MFC 额定满量程。</summary>
    public required double FullScale { get; init; }

    public required string Unit { get; init; }

    public required bool IsRamping { get; init; }

    public required bool IsOffline { get; init; }

    public required bool IsOutOfTolerance { get; init; }

    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>当前 Feedback 数值能不能被业务层信任</summary>
    public required bool IsFeedbackValid { get; init; }

    public required string? DiagnosticCode { get; init; }
}
