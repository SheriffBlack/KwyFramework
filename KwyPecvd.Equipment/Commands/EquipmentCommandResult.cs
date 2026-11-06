namespace KwyPecvd.Equipment.Commands;

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
