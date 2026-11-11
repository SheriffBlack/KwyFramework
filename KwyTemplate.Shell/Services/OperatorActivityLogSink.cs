using KwyTemplate.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace KwyTemplate.Shell.Services;

/// <summary>
/// 通过独立的 ILogger 类别把脱敏活动记录路由到 Operator 文件 Sink。
/// </summary>
public sealed class OperatorActivityLogSink : IOperatorActivitySink
{
    private readonly ILogger<OperatorActivityLogSink> logger;

    public OperatorActivityLogSink(
        ILogger<OperatorActivityLogSink> logger)
    {
        this.logger = logger
            ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task WriteAsync(
        OperatorActivityEntry entry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        cancellationToken.ThrowIfCancellationRequested();

        string eventCode = Normalize(entry.EventCode, "ACTIVITY");
        string source = Normalize(entry.Source, "Application");
        string message = Normalize(entry.Message, "操作已记录。");

        switch (entry.Level)
        {
            case OperatorActivityLevel.Success:
            case OperatorActivityLevel.Information:
                logger.LogInformation(
                    "[{EventCode}] {Source}: {OperatorMessage}",
                    eventCode,
                    source,
                    message);
                break;

            case OperatorActivityLevel.Warning:
                logger.LogWarning(
                    "[{EventCode}] {Source}: {OperatorMessage}",
                    eventCode,
                    source,
                    message);
                break;

            case OperatorActivityLevel.Error:
                logger.LogError(
                    "[{EventCode}] {Source}: {OperatorMessage}",
                    eventCode,
                    source,
                    message);
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(entry),
                    entry.Level,
                    "不支持的操作员活动级别。");
        }

        return Task.CompletedTask;
    }

    private static string Normalize(
        string? value,
        string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return value
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
    }
}
