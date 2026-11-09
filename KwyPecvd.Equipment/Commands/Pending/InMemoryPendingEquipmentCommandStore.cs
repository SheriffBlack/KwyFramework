using System.Collections.Concurrent;

namespace KwyPecvd.Equipment.Commands;

/// <summary>
/// 在当前 RT 进程内保存待完成命令。
/// </summary>
public sealed class InMemoryPendingEquipmentCommandStore : IPendingEquipmentCommandStore
{
    private readonly ConcurrentDictionary< CommandKey,PendingEquipmentCommand> commands = new();

    public bool TryAdd(
        PendingEquipmentCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var key = new CommandKey(
            command.EquipmentModuleId,
            command.CommandTypeCode);

        return commands.TryAdd(key, command);
    }

    public bool TryRemove(
        string equipmentModuleId,
        string commandTypeCode,
        string requestId)
    {
        var key = new CommandKey(
            equipmentModuleId,
            commandTypeCode);

        if (!commands.TryGetValue(
                key,
                out var current))
        {
            return false;
        }

        if (!string.Equals(
                current.RequestId,
                requestId,
                StringComparison.Ordinal))
        {
            return false;
        }

        return commands.TryRemove(
            new KeyValuePair<
                CommandKey,
                PendingEquipmentCommand>(
                key,
                current));
    }

    public bool TryTake(
        string equipmentModuleId,
        string commandTypeCode,
        out PendingEquipmentCommand? command)
    {
        var key = new CommandKey(
            equipmentModuleId,
            commandTypeCode);

        return commands.TryRemove(
            key,
            out command);
    }

    private readonly record struct CommandKey(
        string EquipmentModuleId,
        string CommandTypeCode);
}
