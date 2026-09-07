namespace KwyTemplate.App.Messages;

/// <summary>
/// 生产上下文清空消息。
/// HomeView 在新工单清空或出站成功后发布，点检界面据此清空本轮测量显示状态。
/// </summary>
public sealed record ProductionContextClearedMessage;