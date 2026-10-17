namespace KwyPecvd.Process.Chamber.Thermal;

/// <summary>
/// 腔体热子系统某一时刻的不可变运行快照。
/// </summary>
public sealed record ChamberThermalSnapshot
{
    public required ChamberThermalState State { get; init; }

    public required IReadOnlyCollection<ChamberThermalZoneSnapshot> Zones { get; init; }

    /// <summary>
    /// 所有活动温区同时进入允许范围的开始时间。
    /// 任一温区离开范围后重新清空。
    /// </summary>
    public required DateTimeOffset? StableSince { get; init; }

    public required string? FaultCode { get; init; }

    public required string? FaultMessage { get; init; }

    public required DateTimeOffset Timestamp { get; init; }
}