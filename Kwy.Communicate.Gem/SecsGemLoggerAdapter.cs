using Secs4Net;

namespace Kwy.Communicate.Gem;

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
