namespace KwyPecvd.Equipment.Commands;

/// <summary>
/// 请求初始化指定的设备模块。
/// </summary>
public sealed record InitializeEquipmentCommand(
    string EquipmentModuleId) : IEquipmentCommand;
