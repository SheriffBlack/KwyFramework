using KwyPecvd.Device.PressureGauge;

namespace KwyPecvd.Device.PressureGauge.Io;

public sealed record IoPressureGaugeDefinition
{
    public required PressureGaugeDefinition PressureGauge { get; init; }

    /// <summary>
    /// 压力模拟量输入的稳定逻辑点位ID。
    /// </summary>
    public required string PressureAiId { get; init; }

    /// <summary>
    /// 可选的离线数字输入点位。
    /// </summary>
    public string? OfflineDiId { get; init; }

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(PressureGauge);

        PressureGauge.Validate();

        ArgumentException.ThrowIfNullOrWhiteSpace(PressureAiId);

        if (OfflineDiId is not null)
            ArgumentException.ThrowIfNullOrWhiteSpace(OfflineDiId);
        
    }
}