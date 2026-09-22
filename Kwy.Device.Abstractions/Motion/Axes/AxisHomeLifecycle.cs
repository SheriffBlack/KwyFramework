namespace Kwy.Device.Abstractions.Motion;

/// <summary>业务轴当前回零结果是否仍可用于自动定位。</summary>
public enum AxisHomeValidity
{
    /// <summary>未知状态</summary
    Unknown,
    /// <summary>正在执行回零中</summary
    Homing,
    /// <summary>回零有效，可以使用原点做自动定位</summary
    Valid,
    /// <summary>本次回零动作失败</summary
    Failed,
    /// <summary>原点曾经有效，但现在失效了</summary
    Invalidated
}

/// <summary>回零结果被撤销的原因，供自动模式门禁和现场诊断使用。</summary>
public enum AxisHomeInvalidationReason
{
    /// <summary>运动控制器重连</summary>
    ControllerReconnected,
    /// <summary>伺服使能断开</summary>
    ServoDisabled,
    /// <summary>报警复位（清报警动作）</summary>
    AlarmReset,
    /// <summary>外部独立移动（驱动器端直接运动，非本框架下发指令）</summary>
    ExternalMove,
    /// <summary>软件重启</summary>
    SoftwareRestart,
    /// <summary>手动强制撤销原点有效性</summary>
    Manual
}

/// <summary>业务轴回零可信度的不可变快照。</summary>
public sealed record AxisHomeLifecycleSnapshot(string AxisId, AxisHomeValidity Validity, DateTimeOffset ChangedAt, AxisHomeInvalidationReason? InvalidationReason = null, string? Detail = null);

/// <summary>跨控制器会话维护业务轴回零可信度；自动运动应仅接受 Valid。</summary>
public interface IAxisHomeLifecycle
{
    event Action<AxisHomeLifecycleSnapshot>? Changed;

    AxisHomeLifecycleSnapshot Get(string axisId);

    void Begin(string axisId);

    void Complete(string axisId, bool succeeded, string? detail = null);

    void Invalidate(string axisId, AxisHomeInvalidationReason reason, string? detail = null);
}