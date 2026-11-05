namespace Kwy.Communicate.Gem;

/// <summary>S1F16 OFF-LINE Acknowledge 的 OFLACK 值。</summary>
public enum GemOfflineAcknowledgeCode : byte
{
    /// <summary>Equipment 已接受 Host 的 S1F15 请求并进入 Host Offline。</summary>
    Acknowledged = 0
}
