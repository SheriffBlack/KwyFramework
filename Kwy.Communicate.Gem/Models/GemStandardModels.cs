using Secs4Net;

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
    string? Description = null,
    SecsFormat? Format = null);

public sealed record GemEquipmentConstantDefinition(
    GemEcid Ecid,
    string Name,
    string? Unit = null,
    string? Description = null,
    SecsFormat? Format = null);

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

public sealed record GemRemoteCommandParameterDefinition(
    string Name,
    SecsFormat Format,
    bool Required = true);

public sealed record GemRemoteCommandDefinition(
    string Name,
    IReadOnlyList<GemRemoteCommandParameterDefinition> Parameters,
    IReadOnlySet<GemControlState>? AllowedControlStates = null,
    bool AllowAdditionalParameters = false,
    GemCeid? AcceptedEvent = null,
    GemCeid? CompletedEvent = null,
    GemCeid? FailedEvent = null,
    string? HandlerKey = null)
{
    public string EffectiveHandlerKey => string.IsNullOrWhiteSpace(HandlerKey) ? Name : HandlerKey;
}

public sealed record GemAlarmHistoryItem(
    GemAlarm Alarm,
    DateTimeOffset Timestamp,
    string? Operator = null);

public sealed record GemProcessProgramDefinition(
    string Ppid,
    Item Body,
    GemProcessProgramState State = GemProcessProgramState.Created,
    string? Version = null,
    DateTimeOffset? UpdatedAt = null);

public sealed record GemProcessProgramChangeRecord(
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
    IReadOnlyDictionary<GemVid, Item> Values);

public sealed record GemTraceRunResult(
    uint TraceId,
    uint SamplesReported,
    GemTraceState State,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt);

public sealed record GemRegistryOptions(
    int MaximumAlarmHistory = 1000,
    int MaximumProcessProgramHistory = 1000,
    int MaximumTraceSamples = 10000);

public sealed record GemProcessProgramSaveOptions(bool Overwrite = false);

public sealed record GemProcessProgramSaveResult(
    GemProcessProgramSaveStatus Status,
    string? Message = null)
{
    public bool Succeeded => Status == GemProcessProgramSaveStatus.Saved;
}

public sealed record GemProcessProgramDeleteResult(
    GemProcessProgramDeleteStatus Status,
    string? Message = null)
{
    public bool Succeeded => Status == GemProcessProgramDeleteStatus.Deleted;
}

public sealed record GemSpooledMessage(
    long Sequence,
    SecsMessage Message,
    DateTimeOffset Timestamp);

public sealed record GemSpoolingOptions(
    bool Enabled = false,
    int MaximumMessages = 10000);
