namespace KwyPecvd.Equipment.Commands;

/// <summary>
/// 保存可按 RequestId 查询的 Equipment Command 结果。
/// </summary>
public interface IEquipmentCommandResultStore
{
    void Set(EquipmentCommandResultSnapshot result);

    bool TryGet(string requestId, out EquipmentCommandResultSnapshot? result);
}
