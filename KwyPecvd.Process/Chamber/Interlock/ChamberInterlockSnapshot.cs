namespace KwyPecvd.Process.Chamber.Interlock;

/// <summary>
/// 腔体在某一时刻的联锁输入快照。
/// 数据可由PLC、IO卡或仿真实现提供。
/// </summary>
public sealed record ChamberInterlockSnapshot
{
    /// <summary>联锁数据是否有效。</summary>
    public bool IsValid { get; init; }

    /// <summary>硬件急停安全回路是否健康。</summary>
    public bool EmergencyStopHealthy { get; init; }

    /// <summary>腔门是否确认关闭。</summary>
    public bool DoorClosed { get; init; }

    /// <summary>腔体是否满足工艺真空条件。</summary>
    public bool VacuumReady { get; init; }

    public string? DiagnosticCode { get; init; }

    public DateTimeOffset Timestamp { get; init; }
}
