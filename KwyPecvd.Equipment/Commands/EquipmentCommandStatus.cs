namespace KwyPecvd.Equipment.Commands;

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
