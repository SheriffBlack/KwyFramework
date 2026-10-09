namespace KwyPecvd.Device.Mfc;

/// <summary>
/// 描述实时状态
/// </summary>
public sealed record MfcState
{
    public required string Id { get; init; }

    /// <summary>当前下发给MFC的流量设定值。</summary>
    public double SetPoint { get; init; }

    /// <summary>MFC反馈的实际流量。</summary>
    public double Feedback { get; init; }

    /// <summary>MFC额定满量程。</summary>
    public double FullScale { get; init; }

    public required string Unit { get; init; }

    public bool IsRamping { get; init; }

    public bool IsOffline { get; init; }

    public bool IsOutOfTolerance { get; init; }

    public DateTimeOffset Timestamp { get; init; }

    public bool IsFeedbackValid { get; init; }

    public string? DiagnosticCode { get; init; }
}
