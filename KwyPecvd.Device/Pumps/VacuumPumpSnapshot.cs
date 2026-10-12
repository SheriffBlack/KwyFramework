namespace KwyPecvd.Device.Pumps;

public sealed record VacuumPumpSnapshot
{
    public required string Id { get; init; }

    /// <summary>软件当前要求泵运行。</summary>
    public bool CommandedOn { get; init; }

    /// <summary>泵的实际运行反馈。</summary>
    public bool IsRunning { get; init; }

    public bool HasWarning { get; init; }

    public bool HasAlarm { get; init; }

    public bool IsOverloaded { get; init; }

    public bool IsN2Ready { get; init; }

    public bool IsExhaustReady { get; init; }

    public bool IsOffline { get; init; }

    public bool IsFeedbackValid { get; init; }

    public string? DiagnosticCode { get; init; }

    public DateTimeOffset Timestamp { get; init; }
}
