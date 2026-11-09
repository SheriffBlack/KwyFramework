namespace KwyPecvd.Equipment.Commands;

/// <summary>
/// 请求初始化指定的设备模块。
/// </summary>
public sealed record InitializeEquipmentCommand(string EquipmentModuleId) : IEquipmentCommand;

/// <summary>
/// 请求复位指定设备模块的锁存故障。
/// </summary>
/// <remarks>
/// 表示请求设备重新检查故障条件并清除锁存状态，不代表强制忽略故障。
/// </remarks>
public sealed record ResetEquipmentFaultCommand(string EquipmentModuleId) : IEquipmentCommand;

/// <summary>
/// 请求安全停止指定设备模块的当前工艺。
/// </summary>
public sealed record StopProcessCommand(string EquipmentModuleId) : IEquipmentCommand;
