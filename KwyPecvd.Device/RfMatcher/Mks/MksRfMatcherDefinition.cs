namespace KwyPecvd.Device.RfMatcher.Mks;

/*
 MKS 的双网络模型：一个控制器可以通过 Network = 1/2 分别创建两个逻辑匹配器实例

类似：
    一张运动控制卡
        → 多个 Axis

MksRfMatcherBus 类似运动控制卡，Network 1/2 类似卡里的不同轴。
 */

/// <summary>MKS RF 匹配器静态定义。</summary>
public sealed record MksRfMatcherDefinition
{
    public required RfMatcherDefinition Matcher { get; init; }

    /// <summary>协议地址，编码于报文头高五位。</summary>
    public required byte Address { get; init; }

    /// <summary>同一控制器内的匹配网络编号。</summary>
    public byte Network { get; init; } = 1;

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Matcher);
        Matcher.Validate();

        if (Address > 31)
        {
            throw new ArgumentOutOfRangeException(nameof(Address));
        }

        if (Network is not (1 or 2))
        {
            throw new ArgumentOutOfRangeException(nameof(Network));
        }
    }
}
