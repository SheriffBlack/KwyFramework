namespace KwyPecvd.Device.RfMatcher;

/// <summary>
/// RF匹配器工作模式。
/// </summary>
public enum RfMatcherMode
{
    Unknown = 0,

    /// <summary>
    /// 由上位机指定Tune和Load位置。
    /// </summary>
    Manual = 1,

    /// <summary>
    /// 匹配器自动寻找最佳位置。
    /// </summary>
    Auto = 2
}