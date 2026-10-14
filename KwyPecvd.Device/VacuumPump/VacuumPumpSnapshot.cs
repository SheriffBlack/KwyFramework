namespace KwyPecvd.Device.VacuumPump;

public sealed record VacuumPumpSnapshot
{
    public required string Id { get; init; }

    /// <summary>软件当前要求泵运行。</summary>
    public required bool CommandedOn { get; init; }

    /// <summary>泵的实际运行反馈。</summary>
    public required bool IsRunning { get; init; }

    public required bool HasWarning { get; init; }

    public required bool HasAlarm { get; init; }

    public required bool IsOverloaded { get; init; }

    public required bool IsN2Ready { get; init; }

    public required bool IsExhaustReady { get; init; }

    public required bool IsOffline { get; init; }

    public required bool IsFeedbackValid { get; init; }

    public required string? DiagnosticCode { get; init; }

    public required DateTimeOffset Timestamp { get; init; }
}
