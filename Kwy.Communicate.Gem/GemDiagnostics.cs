namespace Kwy.Communicate.Gem;

/// <summary>
/// 指定 GEM 报文的传输方向。
/// </summary>
public enum GemMessageDirection
{
    /// <summary>接收到的报文。</summary>
    Received,

    /// <summary>发送出的报文。</summary>
    Sent
}

/// <summary>
/// 表示一次可供日志或诊断系统处理的 GEM 报文记录。
/// </summary>
/// <param name="Direction">报文方向。</param>
/// <param name="Stream">SECS Stream 编号。</param>
/// <param name="Function">SECS Function 编号。</param>
/// <param name="ReplyExpected">是否期望回复。</param>
/// <param name="SystemBytes">用于关联请求与响应的系统字节。</param>
/// <param name="Name">报文名称，可为空。</param>
/// <param name="Content">报文正文；未启用正文记录时为空。</param>
/// <param name="Timestamp">记录时间。</param>
public sealed record GemMessageLogEntry(
    GemMessageDirection Direction,
    byte Stream,
    byte Function,
    bool ReplyExpected,
    int SystemBytes,
    string? Name,
    string? Content,
    DateTimeOffset Timestamp);

/// <summary>
/// 定义 GEM 运行诊断输出，不规定日志的保存介质和生命周期。
/// </summary>
public interface IGemDiagnostics
{
    /// <summary>记录跟踪信息。</summary>
    void Trace(string message);

    /// <summary>记录一般信息。</summary>
    void Information(string message);

    /// <summary>记录警告信息。</summary>
    void Warning(string message);

    /// <summary>记录错误信息。</summary>
    void Error(string message, Exception? exception = null);

    /// <summary>记录收到的 GEM 报文。</summary>
    void MessageReceived(GemMessageLogEntry message);

    /// <summary>记录发送的 GEM 报文。</summary>
    void MessageSent(GemMessageLogEntry message);
}

/// <summary>
/// 将 GEM 诊断信息输出到 <see cref="System.Diagnostics.Trace"/>。
/// </summary>
public sealed class TraceGemDiagnostics : IGemDiagnostics
{
    public void Trace(string message) => System.Diagnostics.Trace.WriteLine(message);

    public void Information(string message) => System.Diagnostics.Trace.TraceInformation(message);

    public void Warning(string message) => System.Diagnostics.Trace.TraceWarning(message);

    public void Error(string message, Exception? exception = null)
        => System.Diagnostics.Trace.TraceError(exception is null ? message : $"{message}{Environment.NewLine}{exception}");

    public void MessageReceived(GemMessageLogEntry message) => WriteMessage(message);

    public void MessageSent(GemMessageLogEntry message) => WriteMessage(message);

    private static void WriteMessage(GemMessageLogEntry message)
    {
        var text = $"GEM {message.Direction}: S{message.Stream}F{message.Function}, " +
                   $"W={message.ReplyExpected}, SystemBytes={message.SystemBytes}, Name={message.Name ?? string.Empty}";
        if (!string.IsNullOrEmpty(message.Content))
        {
            text = $"{text}{Environment.NewLine}{message.Content}";
        }

        System.Diagnostics.Trace.WriteLine(text);
    }
}

/// <summary>
/// 丢弃所有 GEM 诊断信息。
/// </summary>
public sealed class NullGemDiagnostics : IGemDiagnostics
{
    public static NullGemDiagnostics Instance { get; } = new();

    private NullGemDiagnostics()
    {
    }

    public void Trace(string message) { }

    public void Information(string message) { }

    public void Warning(string message) { }

    public void Error(string message, Exception? exception = null) { }

    public void MessageReceived(GemMessageLogEntry message) { }

    public void MessageSent(GemMessageLogEntry message) { }
}
