namespace KwyPecvd.Device.Thermal.TemperatureSensor;

/// <summary>
/// 温度传感器的当前不可变快照。
/// </summary>
public sealed record TemperatureSensorSnapshot
{
    public required string Id { get; init; }

    public required double Temperature { get; init; }

    public required string Unit { get; init; }

    public required bool IsOffline { get; init; }

    public required bool IsReadingValid { get; init; }

    public required string? DiagnosticCode { get; init; }

    public required DateTimeOffset Timestamp { get; init; }
}