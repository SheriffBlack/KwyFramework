namespace KwyPecvd.Process.Chamber.Vacuum;

/// <summary>
/// 腔体真空子系统的静态硬件绑定。
/// 工艺压力来源
/// </summary>
public sealed record ChamberVacuumDefinition
{
    public required string PressureGaugeId { get; init; }

    public required string DryPumpId { get; init; }

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(PressureGaugeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(DryPumpId);
    }
}