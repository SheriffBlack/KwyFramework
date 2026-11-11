namespace KwyTemplate.Contracts.Services;

/// <summary>
/// 操作员可见活动的安全级别。
/// </summary>
public enum OperatorActivityLevel
{
    Information,
    Success,
    Warning,
    Error
}

/// <summary>
/// 可持久化给操作员查看的脱敏活动记录。
/// 该模型刻意不提供 Exception 字段，避免异常堆栈进入客户日志。
/// </summary>
public sealed record OperatorActivityEntry(
    DateTimeOffset Timestamp,
    OperatorActivityLevel Level,
    string EventCode,
    string Source,
    string Message);

/// <summary>
/// 持久化操作员可见活动摘要。
/// </summary>
public interface IOperatorActivitySink
{
    Task WriteAsync(
        OperatorActivityEntry entry,
        CancellationToken cancellationToken = default);
}
