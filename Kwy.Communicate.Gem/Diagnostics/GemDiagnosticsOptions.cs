namespace Kwy.Communicate.Gem;

/// <summary>
/// 配置 GEM 报文诊断的详细程度。
/// </summary>
public sealed class GemDiagnosticsOptions
{
    /// <summary>获取或设置是否记录报文收发。</summary>
    public bool LogMessages { get; set; } = true;

    /// <summary>获取或设置是否记录报文正文。默认关闭，以降低性能和敏感信息风险。</summary>
    public bool LogMessageContent { get; set; }

    /// <summary>获取或设置报文正文允许记录的最大字符数。</summary>
    public int MaxMessageContentLength { get; set; } = 4096;

    internal void Validate()
    {
        if (MaxMessageContentLength < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxMessageContentLength),
                MaxMessageContentLength,
                "报文正文最大长度不能小于零。");
        }
    }
}
