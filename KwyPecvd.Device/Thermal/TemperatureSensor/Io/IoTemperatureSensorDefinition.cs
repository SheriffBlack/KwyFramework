namespace KwyPecvd.Device.Thermal.TemperatureSensor.Io;

/// <summary>
/// 基于模拟量输入的温度传感器接线定义。
/// </summary>
public sealed record IoTemperatureSensorDefinition
{
    public required TemperatureSensorDefinition Sensor { get; init; }

    /// <summary>温度反馈模拟量输入点位。</summary>
    public required string TemperatureAiId { get; init; }

    /// <summary>可选的离线数字输入点位。</summary>
    public string? OfflineDiId { get; init; }

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Sensor);

        Sensor.Validate();

        ArgumentException.ThrowIfNullOrWhiteSpace(
            TemperatureAiId);

        if (OfflineDiId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(
                OfflineDiId);
        }
    }
}