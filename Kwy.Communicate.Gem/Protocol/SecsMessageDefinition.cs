namespace Kwy.Communicate.Gem;

/// <summary>
/// 使用 Stream 和 Function 标识一条 SECS-II 消息。
/// </summary>
/// <remarks>
/// SxFy 允许设备自定义扩展，因此使用值对象而不是封闭枚举。
/// </remarks>
public readonly record struct SecsMessageId
{
    public SecsMessageId(byte stream, byte function)
    {
        if (stream > 0x7F)
        {
            throw new ArgumentOutOfRangeException(nameof(stream), stream, "SECS-II Stream 必须介于 0 和 127 之间。");
        }

        Stream = stream;
        Function = function;
    }

    /// <summary>获取 Stream 编号。</summary>
    public byte Stream { get; }

    /// <summary>获取 Function 编号。</summary>
    public byte Function { get; }

    /// <summary>获取该消息是否通常为 Primary Message。</summary>
    public bool IsPrimary => Function % 2 == 1;

    public override string ToString() => $"S{Stream}F{Function}";
}

/// <summary>
/// 定义一条命名的 SECS/GEM 消息及其固定头部语义。
/// </summary>
public sealed record SecsMessageDefinition
{
    public SecsMessageDefinition(SecsMessageId id, string name, bool replyExpected)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Id = id;
        Name = name;
        ReplyExpected = replyExpected;
    }

    /// <summary>获取消息的 SxFy 标识。</summary>
    public SecsMessageId Id { get; }

    /// <summary>获取消息名称。</summary>
    public string Name { get; }

    /// <summary>获取是否期望对端回复。</summary>
    public bool ReplyExpected { get; }

    public override string ToString() => $"{Id} {(ReplyExpected ? "W" : string.Empty)} {Name}".TrimEnd();
}

/// <summary>
/// 集中定义 Kwy GEM 实现当前支持的标准消息。
/// </summary>
public static class GemMessageDefinitions
{
    public static SecsMessageDefinition AreYouThereRequest { get; } =
        new(new SecsMessageId(1, 1), nameof(AreYouThereRequest), true);

    public static SecsMessageDefinition AreYouThereResponse { get; } =
        new(new SecsMessageId(1, 2), nameof(AreYouThereResponse), false);

    public static SecsMessageDefinition SelectedEquipmentStatusRequest { get; } =
        new(new SecsMessageId(1, 3), nameof(SelectedEquipmentStatusRequest), true);

    public static SecsMessageDefinition SelectedEquipmentStatusData { get; } =
        new(new SecsMessageId(1, 4), nameof(SelectedEquipmentStatusData), false);

    public static SecsMessageDefinition EstablishCommunicationsRequest { get; } =
        new(new SecsMessageId(1, 13), nameof(EstablishCommunicationsRequest), true);

    public static SecsMessageDefinition HostCommandSend { get; } =
        new(new SecsMessageId(2, 41), nameof(HostCommandSend), true);

    public static SecsMessageDefinition AlarmReportSend { get; } =
        new(new SecsMessageId(5, 1), nameof(AlarmReportSend), true);

    public static SecsMessageDefinition TraceDataSend { get; } =
        new(new SecsMessageId(6, 1), nameof(TraceDataSend), true);

    public static SecsMessageDefinition EventReportSend { get; } =
        new(new SecsMessageId(6, 11), nameof(EventReportSend), true);

    public static SecsMessageDefinition ProcessProgramLoadInquire { get; } =
        new(new SecsMessageId(7, 1), nameof(ProcessProgramLoadInquire), true);

    public static SecsMessageDefinition ProcessProgramSend { get; } =
        new(new SecsMessageId(7, 3), nameof(ProcessProgramSend), true);

    public static SecsMessageDefinition ProcessProgramRequest { get; } =
        new(new SecsMessageId(7, 5), nameof(ProcessProgramRequest), true);

    public static SecsMessageDefinition TerminalRequest { get; } =
        new(new SecsMessageId(10, 1), nameof(TerminalRequest), true);
}
