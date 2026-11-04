namespace Kwy.Communicate.Gem;

/// <summary>配置 GEM 诊断事件的详细程度。</summary>
public sealed class GemDiagnosticsOptions
{
    /// <summary>获取是否发布报文诊断事件。</summary>
    public bool IncludeMessages { get; init; } = true;

    /// <summary>获取报文诊断事件是否包含报文正文。</summary>
    public bool IncludeMessageContent { get; init; }

    /// <summary>获取报文正文可包含的最大字符数。</summary>
    public int MaximumMessageContentLength { get; init; } = 4096;

    internal void Validate()
    {
        if (MaximumMessageContentLength < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumMessageContentLength),
                MaximumMessageContentLength,
                "报文正文最大长度不能小于 0。");
        }
    }
}
