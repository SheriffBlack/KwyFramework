using Secs4Net;

namespace Kwy.Communicate.Gem;

public sealed record GemVariable(uint Id, string Name, Item Value, string? Unit = null);

public sealed record GemEquipmentConstant(uint Id, string Name, Item Value, Item? Min = null, Item? Max = null, string? Unit = null);

public sealed record GemReport(uint ReportId, IReadOnlyList<uint> VariableIds);

public sealed record GemCollectionEvent(uint EventId, string Name, IReadOnlyList<uint> LinkedReportIds);

public sealed record GemAlarm(uint AlarmId, string Text, GemAlarmState State, byte AlarmCode = 0);

public sealed record GemRemoteCommand(string CommandName, IReadOnlyDictionary<string, Item> Parameters);

public sealed record GemRemoteCommandResult(GemAckCode AckCode, string? Message = null);

public sealed record GemRecipe(string Ppid, Item Body, string? Version = null);

public sealed record GemTerminalMessage(string Text, byte TerminalId = 0);
