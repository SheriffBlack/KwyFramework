using KwyTemplate.App.Models;

namespace KwyTemplate.App.Messages;

/// <summary>
/// 自动点检配置变更消息。
/// 设置页保存点检配置后发布，运行时监控据此刷新时间窗口。
/// </summary>
public sealed record CompensateOptionsChangedMessage(CompensateOptions Options);