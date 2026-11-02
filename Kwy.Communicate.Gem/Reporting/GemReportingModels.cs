using Secs4Net;

namespace Kwy.Communicate.Gem;

/// <summary>报警设置或清除状态，通过 S5F1 上报。</summary>
public enum GemAlarmState
{
    /// <summary>报警已激活。</summary>
    Set,
    /// <summary>报警已清除。</summary>
    Clear
}

public sealed record GemVariable(uint Id, string Name, Item Value, string? Unit = null);
public sealed record GemEquipmentConstant(uint Id, string Name, Item Value, Item? Min = null, Item? Max = null, string? Unit = null);
public sealed record GemAlarm(uint AlarmId, string Text, GemAlarmState State, byte AlarmCode = 0);
public sealed record GemAlarmHistoryItem(GemAlarm Alarm, DateTimeOffset Timestamp, string? Operator = null);
public sealed record GemTerminalMessage(string Text, byte TerminalId = 0);
