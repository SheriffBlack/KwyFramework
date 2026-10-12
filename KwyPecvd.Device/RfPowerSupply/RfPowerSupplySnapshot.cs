namespace KwyPecvd.Device.RfPowerSupply;

public sealed record RfPowerSupplySnapshot
{
    public required string Id { get; init; }

    public required double SetPoint { get; init; }

    public required double ForwardPower { get; init; }

    public required double ReflectedPower { get; init; }

    public required string Unit { get; init; }

    public required bool CommandedEnabled { get; init; }

    public required bool IsOutputOn { get; init; }

    public required bool IsOffline { get; init; }

    public required bool IsFeedbackValid { get; init; }

    public required bool IsFaulted { get; init; }

    public required bool IsReflectedPowerHigh { get; init; }

    public required bool IsOutOfTolerance { get; init; }

    public required string? DiagnosticCode { get; init; }

    public required DateTimeOffset Timestamp { get; init; }
}