namespace Kwy.Communicate.Gem;

/// <summary>将 GEM 诊断信息输出到 <see cref="System.Diagnostics.Trace"/>。</summary>
/// <remarks>适合开发、学习和临时联调；生产环境应注入项目自有实现。</remarks>
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
        string text = $"GEM {message.Direction}: S{message.Stream}F{message.Function}, " +
                      $"W={message.ReplyExpected}, SystemBytes={message.SystemBytes}, Name={message.Name ?? string.Empty}";
        if (!string.IsNullOrEmpty(message.Content))
            text = $"{text}{Environment.NewLine}{message.Content}";
        System.Diagnostics.Trace.WriteLine(text);
    }
}
