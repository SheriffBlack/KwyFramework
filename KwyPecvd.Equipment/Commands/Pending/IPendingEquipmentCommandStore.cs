namespace KwyPecvd.Equipment.Commands;

/// <summary>
/// 保存已接受但尚未完成的 Equipment Command。
/// </summary>
public interface IPendingEquipmentCommandStore
{
    /// <summary>命令准备执行前占据位置 </summary>
    bool TryAdd(PendingEquipmentCommand command);

    /// <summary>命令被 Process 拒绝，撤销待完成记录 </summary>
    bool TryRemove(
        string equipmentModuleId,
        string commandTypeCode,
        string requestId);

    /// <summary>命令被 Process 拒绝，撤销待完成记录 </summary>
    bool TryTake(
        string equipmentModuleId,
        string commandTypeCode,
        out PendingEquipmentCommand? command);
}