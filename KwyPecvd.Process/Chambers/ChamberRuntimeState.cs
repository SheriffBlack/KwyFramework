using System.Collections.Immutable;

namespace KwyPecvd.Process.Chambers;

/// <summary>腔体控制器内部的不可变运行状态。</summary>
internal sealed record ChamberRuntimeState
{
    /// <summary>
    /// 不可变集合。
    /// 当前没有任务时使用的空集合
    /// </summary>
    public static IReadOnlyDictionary<string, ChamberGasTarget>
        EmptyGasTargets
    { get; } =
        ImmutableDictionary
            .Create<string, ChamberGasTarget>(StringComparer.OrdinalIgnoreCase);

    public ChamberState State { get; init; } =
        ChamberState.Offline;

    public DateTimeOffset StateChangedAt { get; init; } =
        DateTimeOffset.UtcNow;

    public string? FaultCode { get; init; }

    public string? FaultMessage { get; init; }

    /// <summary>停止命令全部接受的时刻，反馈必须在此之后刷新。</summary>
    public DateTimeOffset? StopCommandsCompletedAt { get; init; }

    /*
    StartGasPreparationAsync
        │
        │ 保存目标
        ▼
    ActiveGasTargets
            │
            │ 后续每个周期读取
            ▼
    ExecuteCycleAsync
            │
            ├─ 尚未达到目标 → Preparing
            ├─ 全部达到目标 → Processing
            └─ 反馈异常     → Faulted
     */
    /// <summary>
    /// 本轮工艺有目标的气路
    /// 当前任务跨多个RT周期需要持续追踪的气体目标。
    /// ChamberController 状态机正在执行的目标上下文
    /// </summary>
    public IReadOnlyDictionary<string, ChamberGasTarget>
        ActiveGasTargets
    { get; init; } =
        EmptyGasTargets;
}
