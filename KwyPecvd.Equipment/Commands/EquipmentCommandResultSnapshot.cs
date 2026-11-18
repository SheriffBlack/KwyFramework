namespace KwyPecvd.Equipment.Commands;

/// <summary>
/// 用于按 RequestId 查询命令当前处理结果。
/// </summary>
public sealed record EquipmentCommandResultSnapshot(
    string RequestId,
    string CommandTypeCode,
    string EquipmentModuleId,
    EquipmentCommandStatus Status,
    string Code,
    string Message,
    DateTimeOffset UpdatedAt);
