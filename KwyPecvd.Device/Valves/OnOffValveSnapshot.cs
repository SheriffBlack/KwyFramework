namespace KwyPecvd.Device.Valves;

public sealed record OnOffValveSnapshot
{
    public required string Id { get; init; }

    public required OnOffValvePosition  CommandedPosition { get; init; }

    public required OnOffValveState State { get; init; }

    public required bool IsOffline { get; init; }

    public required bool IsFeedbackValid { get; init; }

    public required bool IsMotionTimedOut { get; init; }

    public required string? DiagnosticCode { get; init; }

    public required DateTimeOffset Timestamp { get; init; }
}