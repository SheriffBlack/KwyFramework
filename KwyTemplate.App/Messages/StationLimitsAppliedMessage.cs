namespace KwyTemplate.App.Messages;

/// <summary>
/// 仪表参数应用完成消息。
/// SetView 中本地仪表参数应用成功后发布，HomeView 据此把当前 TestLimits 刷新到表格和图表。
/// </summary>
public sealed record StationLimitsAppliedMessage;