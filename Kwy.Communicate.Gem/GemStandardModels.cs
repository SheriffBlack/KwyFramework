using Kwy.Communicate.Secs;

namespace Kwy.Communicate.Gem;

public readonly record struct GemCeid(uint Value);

public readonly record struct GemRptid(uint Value);

public readonly record struct GemVid(uint Value);

public readonly record struct GemAlid(uint Value);

public readonly record struct GemEcid(uint Value);

public sealed record GemVariableDefinition(
    GemVid Vid,
    string Name,
    GemVariableKind Kind,
    string? Unit = null,
    string? Description = null);

public sealed record GemReportDefinition(
    GemRptid Rptid,
    IReadOnlyList<GemVid> VariableIds);

public sealed record GemCollectionEventDefinition(
    GemCeid Ceid,
    string Name,
    IReadOnlyList<GemRptid> LinkedReports,
    bool Enabled = true);

public sealed record GemAlarmDefinition(
    GemAlid Alid,
    string Code,
    string Text,
    byte AlarmCode = 0,
    bool Enabled = true);

public sealed record GemAlarmHistoryItem(
    GemAlarm Alarm,
    DateTimeOffset Timestamp,
    string? Operator = null);

public sealed record GemRecipeDefinition(
    string Ppid,
    SecsItem Body,
    GemRecipeState State = GemRecipeState.Created,
    string? Version = null,
    DateTimeOffset? UpdatedAt = null);

public sealed record GemRecipeChangeRecord(
    string Ppid,
    string Action,
    string Operator,
    DateTimeOffset Timestamp,
    string? Reason = null);

public sealed record GemTraceDefinition(
    uint TraceId,
    TimeSpan SampleInterval,
    uint TotalSamples,
    IReadOnlyList<GemVid> VariableIds,
    GemTraceState State = GemTraceState.Enabled);

public sealed record GemTraceSample(
    uint TraceId,
    uint SampleNumber,
    DateTimeOffset Timestamp,
    IReadOnlyDictionary<GemVid, SecsItem> Values);

public sealed record GemSpooledMessage(
    long Sequence,
    SecsMessage Message,
    DateTimeOffset Timestamp);

public sealed record GemSpoolingOptions(
    bool Enabled = false,
    int MaximumMessages = 10000);
