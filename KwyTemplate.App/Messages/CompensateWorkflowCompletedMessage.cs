namespace KwyTemplate.App.Messages;

/// <summary>
/// 点检流程全部完成消息。
/// 只有所有点检流程项完成，并且 MES 点检数据保存成功后才发布。
/// </summary>
public sealed record CompensateWorkflowCompletedMessage(
    DateTimeOffset CompletedAt,
    string? WorkOrderNo,
    string? EquipmentNo);