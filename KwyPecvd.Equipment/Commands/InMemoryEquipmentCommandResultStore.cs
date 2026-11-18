using System.Collections.Concurrent;

namespace KwyPecvd.Equipment.Commands;

/// <summary>
/// 在当前 RT 进程内保存命令查询结果。
/// </summary>
public sealed class InMemoryEquipmentCommandResultStore : IEquipmentCommandResultStore
{
    private readonly ConcurrentDictionary<
        string,
        EquipmentCommandResultSnapshot> results =
            new(StringComparer.Ordinal);

    public void Set(EquipmentCommandResultSnapshot result)
    {
        ArgumentNullException.ThrowIfNull(result);

        results.AddOrUpdate(
            result.RequestId,
            result,
            (_, current) => CanReplace(current, result)
                ? result
                : current);
    }

    public bool TryGet(string requestId, out EquipmentCommandResultSnapshot? result)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);

        return results.TryGetValue(
            requestId,
            out result);
    }

    private static bool CanReplace(
        EquipmentCommandResultSnapshot current,
        EquipmentCommandResultSnapshot next)
    {
        if (current.Status is
            EquipmentCommandStatus.Completed or
            EquipmentCommandStatus.Failed or
            EquipmentCommandStatus.Rejected)
        {
            return false;
        }

        return next.UpdatedAt >= current.UpdatedAt;
    }
}
