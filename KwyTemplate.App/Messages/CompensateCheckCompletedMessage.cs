namespace KwyTemplate.App.Messages;

/// <summary>点检成功保存后的时间事实，用于判定是否在当前排程窗口内完成。</summary>
public sealed record CompensateCheckCompletedMessage(DateTimeOffset CompletedAt);
