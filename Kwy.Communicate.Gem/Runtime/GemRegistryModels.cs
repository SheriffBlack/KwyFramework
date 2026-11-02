namespace Kwy.Communicate.Gem;

/// <summary>
/// 记录上传、删除等变更历史
/// </summary>
/// <param name="Ppid"></param>
/// <param name="Action"></param>
/// <param name="Operator"></param>
/// <param name="Timestamp"></param>
/// <param name="Reason"></param>
public sealed record GemProcessProgramChangeRecord(
    string Ppid,
    string Action,
    string Operator,
    DateTimeOffset Timestamp,
    string? Reason = null);

public sealed record GemRegistryOptions(
    int MaximumAlarmHistory = 1000,
    int MaximumProcessProgramHistory = 1000,
    int MaximumTraceSamples = 10000);
