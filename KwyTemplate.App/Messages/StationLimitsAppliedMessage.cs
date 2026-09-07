namespace KwyTemplate.App.Messages;

/// <summary>
/// 仪表参数更新完成消息。
/// <paramref name="SynchronizeCorrectionFrequency" /> 仅由 SetView 的人工“应用”发布，
/// 用于让校正页放弃临时手工频率并与新下发的仪表参数重新对齐。
/// </summary>
public sealed record StationLimitsAppliedMessage(bool SynchronizeCorrectionFrequency = false);
