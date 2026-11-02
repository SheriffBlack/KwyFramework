namespace Kwy.Communicate.Gem;

/// <summary>定义 GEM 运行诊断输出，不规定日志的保存介质和生命周期。</summary>
public interface IGemDiagnostics
{
    void Trace(string message);

    void Information(string message);

    void Warning(string message);

    void Error(string message, Exception? exception = null);

    void MessageReceived(GemMessageLogEntry message);

    void MessageSent(GemMessageLogEntry message);
}
