namespace KwyPecvd.Equipment.Commands;

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
    /// 设备后台自身发起的命令。
    /// </summary>
    System
}