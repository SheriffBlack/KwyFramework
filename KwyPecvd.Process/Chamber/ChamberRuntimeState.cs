namespace KwyPecvd.Process.Chamber;

/// <summary>腔体控制器内部的不可变运行状态。</summary>
internal sealed record ChamberRuntimeState
{
    public ChamberState State { get; init; } = ChamberState.Offline;

    public DateTimeOffset StateChangedAt { get; init; } =  DateTimeOffset.UtcNow;

    public string? FaultCode { get; init; }

    public string? FaultMessage { get; init; }

    /// <summary>停止命令全部接受的时刻，反馈必须在此之后刷新。</summary>
    public DateTimeOffset? StopCommandsCompletedAt { get; init; }

    /// <summary>
    /// 当前跨多个 RT 周期执行的准备任务。
    /// 仅在 Preparing 状态中存在。
    /// </summary>
    public ChamberPreparationContext? Preparation { get; init; }
}
