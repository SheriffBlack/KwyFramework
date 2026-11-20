using System.Collections.Concurrent;

namespace KwyPecvd.Equipment.Commands;

/// <summary>
/// 在当前 RT 进程内保存命令查询结果。
/// </summary>
public sealed class InMemoryEquipmentCommandResultStore : IEquipmentCommandResultStore
{
    private readonly ConcurrentDictionary<string, EquipmentCommandResultSnapshot> results =
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

    public int RemoveTerminalResultsOlderThan(DateTimeOffset cutoff)
    {
        var removedCount = 0;

        foreach (var pair in results)
        {
            var result = pair.Value;

            if (!IsTerminal(result.Status))
            {
                continue;
            }

            if (result.UpdatedAt >= cutoff)
            {
                continue;
            }

            // 只有字典中的值仍然是我刚才检查过的那个快照时，才删除。
            if (results.TryRemove(
                    new KeyValuePair<
                        string,
                        EquipmentCommandResultSnapshot>(
                        pair.Key,
                        result)))
            {
                removedCount++;
            }
        }

        return removedCount;
    }

    private static bool CanReplace(EquipmentCommandResultSnapshot current, EquipmentCommandResultSnapshot next)
    {
        if (IsTerminal(current.Status))
        {
            return false;
        }

        return next.UpdatedAt >= current.UpdatedAt;
    }

    private static bool IsTerminal(EquipmentCommandStatus status)
    {
        return status is
            EquipmentCommandStatus.Completed or
            EquipmentCommandStatus.Rejected or
            EquipmentCommandStatus.Failed;
    }
}
