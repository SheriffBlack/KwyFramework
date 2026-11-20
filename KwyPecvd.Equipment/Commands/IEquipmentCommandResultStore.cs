namespace KwyPecvd.Equipment.Commands;

/// <summary>
/// 保存可按 RequestId 查询的 Equipment Command 结果。
/// </summary>
public interface IEquipmentCommandResultStore
{
    void Set(EquipmentCommandResultSnapshot result);

    bool TryGet(string requestId, out EquipmentCommandResultSnapshot? result);

    /// <summary>
    /// 删除更新时间早于指定时间的终态结果。
    /// Accepted等非终态结果不会被删除。
    /// </summary>
    /// <returns>实际删除的结果数量。</returns>
    int RemoveTerminalResultsOlderThan(DateTimeOffset cutoff);
}
