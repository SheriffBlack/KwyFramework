using Secs4Net;

namespace Kwy.Communicate.Gem;

/// <summary>GEM Spool 的运行状态。</summary>
/// <remarks>可在 GEM 通信不可用时缓存允许的上报报文，并在通信恢复后按序补传。</remarks>
public enum GemSpoolingState
{
    /// <summary>Spool 功能已关闭。</summary>
    Disabled,
    /// <summary>Spool 功能已开启，当前没有待补传报文。</summary>
    Enabled,
    /// <summary>存在待补传报文。</summary>
    Active,
    /// <summary>正在向 Host 按序补传。</summary>
    Transmitting,
    /// <summary>正在清空 Spool。</summary>
    Purging
}

/// <summary>Spool 达到容量上限时的处理策略。</summary>
public enum GemSpoolOverflowPolicy
{
    /// <summary>保留现有报文并拒绝新报文。</summary>
    RejectNewest,
    /// <summary>移除最旧报文后接收新报文。</summary>
    DropOldest
}

public sealed record GemSpooledMessage(long Sequence, SecsMessage Message, DateTimeOffset Timestamp);

public sealed record GemSpoolingOptions(
    bool Enabled = false,
    int MaximumMessages = 10000,
    bool IncludeAlarms = true,
    bool IncludeTraceData = false,
    bool IncludeTerminalMessages = false,
    bool SpoolOnAmbiguousSendFailure = false,
    GemSpoolOverflowPolicy OverflowPolicy = GemSpoolOverflowPolicy.RejectNewest);

public sealed record GemSpoolReplayResult(int Attempted, int Sent, int Remaining, Exception? Error = null)
{
    public bool Succeeded => Error is null;
}
