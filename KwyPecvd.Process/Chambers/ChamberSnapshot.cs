using KwyPecvd.Device.Mfc;

namespace KwyPecvd.Process.Chambers;

/// <summary>某一时刻的腔室聚合运行快照。</summary>
public sealed record ChamberSnapshot
{
    public required string ChamberId { get; init; }

    /// <summary>腔体模块当前运行状态。</summary>
    public ChamberState State { get; init; }

    /// <summary>最近一次状态变化时间。</summary>
    public DateTimeOffset StateChangedAt { get; init; }

    public string? FaultCode { get; init; }

    public string? FaultMessage { get; init; }

    public IReadOnlyCollection<ChamberGasFlowSnapshot> GasFlows { get; init; } = Array.Empty<ChamberGasFlowSnapshot>();

    public DateTimeOffset Timestamp { get; init; }
}

public sealed record ChamberGasFlowSnapshot
{
    public required string GasName { get; init; }

    public required MfcSnapshot Mfc { get; init; }
}
