namespace Kwy.Communicate.Gem;

/// <summary>使用 Stream 和 Function 标识一条 SECS-II 消息。</summary>
/// <remarks>SxFy 允许设备自定义扩展，因此使用值对象而不是封闭枚举。</remarks>
public readonly record struct SecsMessageId
{
    public SecsMessageId(byte stream, byte function)
    {
        if (stream > 0x7F)
            throw new ArgumentOutOfRangeException(nameof(stream), stream, "SECS-II Stream 必须介于 0 和 127 之间。");
        Stream = stream;
        Function = function;
    }

    public byte Stream { get; }

    public byte Function { get; }

    public bool IsPrimary => Function % 2 == 1;

    public override string ToString() => $"S{Stream}F{Function}";
}

/// <summary>定义一条命名的 SECS/GEM 消息及其固定头部语义。</summary>
public sealed record SecsMessageDefinition
{
    public SecsMessageDefinition(SecsMessageId id, string name, bool replyExpected)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Id = id;
        Name = name;
        ReplyExpected = replyExpected;
    }

    public SecsMessageId Id { get; }

    public string Name { get; }

    public bool ReplyExpected { get; }

    public override string ToString() => $"{Id} {(ReplyExpected ? "W" : string.Empty)} {Name}".TrimEnd();
}

/// <summary>集中定义 Kwy GEM 实现当前支持的标准消息。</summary>
public static partial class GemMessageDefinitions
{
}
