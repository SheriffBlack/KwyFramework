namespace Kwy.Communicate.Gem;

/// <summary>指定 GEM 报文的传输方向。</summary>
public enum GemMessageDirection
{
    /// <summary>收到：设备侧收到 Host 下发的 SECS 消息（入站） </summary>
    Received,
    /// <summary>发送：设备侧主动发给 Host 的 SECS 消息（出站） </summary>
    Sent
}

/// <summary>表示一次可供日志或诊断系统处理的 GEM 报文记录。</summary>
public sealed record GemMessageLogEntry(
    GemMessageDirection Direction,
    byte Stream,
    byte Function,
    bool ReplyExpected,
    int SystemBytes,
    string? Name,
    string? Content,
    DateTimeOffset Timestamp);
