namespace Kwy.Communicate.Gem;

public static partial class GemMessageDefinitions
{
    public static SecsMessageDefinition TraceDataSend { get; } =
        new(new SecsMessageId(6, 1), nameof(TraceDataSend), true);
    public static SecsMessageDefinition EventReportSend { get; } =
        new(new SecsMessageId(6, 11), nameof(EventReportSend), true);
}
