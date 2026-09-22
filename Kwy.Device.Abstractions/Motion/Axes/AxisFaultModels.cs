namespace Kwy.Device.Abstractions.Motion;

/// <summary>业务与报警系统使用的轴故障类别，不直接暴露厂商状态字位定义。</summary>
public enum AxisFaultCode
{
    ControllerAlarm,
    PositiveLimitReached,
    NegativeLimitReached,
    ServoDisabled,
    FollowingError,
    HomingFailed,
    ControllerCommunication
}

/// <summary>故障对运动权限的影响等级；硬件急停回路不由软件故障等级替代。</summary>
public enum AxisFaultSeverity
{
    Warning,
    StopRequired,
    SafetyCritical
}

/// <summary>
/// 已从厂商状态字、错误码映射出的强类型故障。
/// RawCode 仅用于现场诊断和厂商支持，业务流程应依据 Code 与 Severity 决策。
/// </summary>
public sealed record AxisFault(
    AxisFaultCode Code,
    AxisFaultSeverity Severity,
    string Message,
    int? RawCode = null,
    string? RecoveryAdvice = null);

/// <summary>提供物理轴当前故障的强类型视图；厂商适配器可覆盖基础映射并补充专属错误码。</summary>
public interface IAxisFaultReader
{
    AxisFault? GetAxisFault(short axis);
}
