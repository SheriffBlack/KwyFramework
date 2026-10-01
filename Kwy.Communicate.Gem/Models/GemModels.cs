using Secs4Net;

namespace Kwy.Communicate.Gem;

public sealed record GemVariable(uint Id, string Name, Item Value, string? Unit = null);

public sealed record GemEquipmentConstant(uint Id, string Name, Item Value, Item? Min = null, Item? Max = null, string? Unit = null);

public sealed record GemReport(uint ReportId, IReadOnlyList<uint> VariableIds);

public sealed record GemCollectionEvent(uint EventId, string Name, IReadOnlyList<uint> LinkedReportIds);

public sealed record GemAlarm(uint AlarmId, string Text, GemAlarmState State, byte AlarmCode = 0);

public sealed record GemRemoteCommand(string CommandName, IReadOnlyDictionary<string, Item> Parameters)
{
    public bool TryGetParameter(string name, out Item value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        foreach ((string key, Item item) in Parameters)
        {
            if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
            {
                value = item;
                return true;
            }
        }

        value = null!;
        return false;
    }

    public Item GetRequiredParameter(string name)
        => TryGetParameter(name, out Item? value)
            ? value
            : throw new KeyNotFoundException($"Remote command parameter '{name}' is missing.");
}

public sealed record GemRemoteCommandResult(
    GemAckCode AckCode,
    string? Message = null,
    GemRemoteCommandCompletionStatus CompletionStatus = GemRemoteCommandCompletionStatus.Unspecified)
{
    public bool IsAccepted => AckCode == GemAckCode.Accepted;

    public bool IsCompleted => CompletionStatus == GemRemoteCommandCompletionStatus.Completed;
}

public sealed record GemProcessProgram(
    string Ppid,
    Item Body,
    string? Version = null,
    DateTimeOffset? UpdatedAt = null);

public sealed record GemTerminalMessage(string Text, byte TerminalId = 0);
