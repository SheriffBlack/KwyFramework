namespace KwyPecvd.Equipment.Commands;

/// <summary>
/// 表示一个与通信协议无关的设备业务命令。
/// </summary>
public interface IEquipmentCommand;

/// <summary>
/// 表示设备命令来自哪里。
/// </summary>
public enum EquipmentCommandSource
{
    /// <summary>
    /// 本地 HMI 操作员提交的命令。
    /// </summary>
    LocalUi,

    /// <summary>
    /// Factory Host 通过 SECS/GEM 提交的命令。
    /// </summary>
    FactoryHost,

    /// <summary>
    /// 诊断或临时联调入口提交的命令。
    /// </summary>
    Diagnostic,

    /// <summary>
    /// 设备后台自身发起的命令。
    /// </summary>
    System
}

/// <summary>
/// 表示一次 Equipment Command 的调用上下文。
/// </summary>
public sealed record EquipmentCommandContext(
    string RequestId,
    EquipmentCommandSource Source,
    string Requester,
    DateTimeOffset RequestedAt);

/// <summary>
/// 包装命令调用上下文和业务命令。
/// </summary>
public sealed record EquipmentCommandEnvelope(EquipmentCommandContext Context, IEquipmentCommand Command);

/// <summary>
/// 表示设备命令的业务处理状态。
/// </summary>
public enum EquipmentCommandStatus
{
    /// <summary>
    /// 命令已可靠接收，执行尚未完成。
    /// </summary>
    Accepted,

    /// <summary>
    /// 命令已经执行完成。
    /// </summary>
    Completed,

    /// <summary>
    /// 命令未被设备接受。
    /// </summary>
    Rejected,

    /// <summary>
    /// 命令被接受后执行失败。
    /// </summary>
    Failed
}

/// <summary>
/// 表示设备命令的处理结果。
/// </summary>
public sealed record EquipmentCommandResult(
    string RequestId,
    EquipmentCommandStatus Status,
    string Code,
    string Message)
{
    public bool Succeeded => Status is EquipmentCommandStatus.Accepted or EquipmentCommandStatus.Completed;
}
