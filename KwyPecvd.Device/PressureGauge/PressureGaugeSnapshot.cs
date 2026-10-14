namespace KwyPecvd.Device.PressureGauge;

public sealed record PressureGaugeSnapshot
{
    public required string Id { get; init; }

    public required double Pressure { get; init; }

    public required string Unit { get; init; }

    public required bool IsOffline { get; init; }

    public required bool IsReadingValid { get; init; }

    public required string? DiagnosticCode { get; init; }

    public required DateTimeOffset Timestamp { get; init; }
}
