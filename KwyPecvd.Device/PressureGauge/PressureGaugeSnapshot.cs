namespace KwyPecvd.Device.PressureGauge;

public sealed record PressureGaugeSnapshot
{
    public required string Id { get; init; }

    public double Pressure { get; init; }

    public required string Unit { get; init; }

    public bool IsOffline { get; init; }

    public bool IsReadingValid { get; init; }

    public string? DiagnosticCode { get; init; }

    public DateTimeOffset Timestamp { get; init; }
}