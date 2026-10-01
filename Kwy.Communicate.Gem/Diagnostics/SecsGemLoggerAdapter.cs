using Secs4Net;

namespace Kwy.Communicate.Gem;

/// <summary>
/// Secs4Net 日志回调与 Kwy GEM 诊断体系之间的内部适配器。
/// </summary>
/// <remarks>
/// 负责将 Secs4Net 提供的消息、方向、系统字节和异常信息转换为
/// <see cref="GemMessageLogEntry"/>，并根据诊断配置处理消息体和长度限制，
/// 最终转发给 <see cref="IGemDiagnostics"/>。
///
/// 本类型不负责日志输出或持久化；具体处理方式由
/// <see cref="IGemDiagnostics"/> 的实现决定。
/// </remarks>
internal sealed class SecsGemLoggerAdapter : ISecsGemLogger
{
    private readonly IGemDiagnostics diagnostics;
    private readonly GemDiagnosticsOptions options;

    public SecsGemLoggerAdapter(IGemDiagnostics diagnostics, GemDiagnosticsOptions options)
    {
        this.diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        options.Validate();
    }

    public void Debug(string message) => diagnostics.Trace(message);

    public void Info(string message) => diagnostics.Information(message);

    public void Warning(string message) => diagnostics.Warning(message);

    public void Error(string message) => diagnostics.Error(message);

    public void Error(string message, Exception exception) => diagnostics.Error(message, exception);

    public void Error(string message, SecsMessage? secsMessage, Exception? exception)
        => diagnostics.Error(
            secsMessage is null ? message : $"{message} ({Describe(secsMessage)})",
            exception);

    public void MessageIn(SecsMessage message, int systemBytes)
    {
        if (options.LogMessages)
        {
            diagnostics.MessageReceived(CreateEntry(message, systemBytes, GemMessageDirection.Received));
        }
    }

    public void MessageOut(SecsMessage message, int systemBytes)
    {
        if (options.LogMessages)
        {
            diagnostics.MessageSent(CreateEntry(message, systemBytes, GemMessageDirection.Sent));
        }
    }

    private GemMessageLogEntry CreateEntry(
        SecsMessage message,
        int systemBytes,
        GemMessageDirection direction)
        => new(
            direction,
            message.S,
            message.F,
            message.ReplyExpected,
            systemBytes,
            message.Name,
            GetContent(message),
            DateTimeOffset.Now);

    private string? GetContent(SecsMessage message)
    {
        if (!options.LogMessageContent || message.SecsItem is null)
        {
            return null;
        }

        var content = message.SecsItem.ToString() ?? string.Empty;
        if (content.Length <= options.MaxMessageContentLength)
        {
            return content;
        }

        return string.Concat(content.AsSpan(0, options.MaxMessageContentLength), "…");
    }

    private static string Describe(SecsMessage message)
        => $"S{message.S}F{message.F}, W={message.ReplyExpected}, Name={message.Name ?? string.Empty}";
}
