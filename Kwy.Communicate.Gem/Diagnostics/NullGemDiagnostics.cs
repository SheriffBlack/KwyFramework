namespace Kwy.Communicate.Gem;

/// <summary>丢弃所有 GEM 诊断信息。</summary>
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
