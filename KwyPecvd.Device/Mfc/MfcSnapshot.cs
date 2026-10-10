namespace KwyPecvd.Device.Mfc;

/// <summary>某一时刻的 MFC 不可变运行快照。</summary>
public sealed record MfcSnapshot
{
    public required string Id { get; init; }

    /// <summary>当前下发给 MFC 的流量设定值。</summary>
    public double SetPoint { get; init; }

    /// <summary>MFC 反馈的实际流量。</summary>
    public double Feedback { get; init; }

    /// <summary>MFC 额定满量程。</summary>
    public double FullScale { get; init; }

    public required string Unit { get; init; }

    public bool IsRamping { get; init; }

    public bool IsOffline { get; init; }

    public bool IsOutOfTolerance { get; init; }

    public DateTimeOffset Timestamp { get; init; }

    /// <summary>当前 Feedback 数值能不能被业务层信任</summary>
    public bool IsFeedbackValid { get; init; }

    public string? DiagnosticCode { get; init; }
}