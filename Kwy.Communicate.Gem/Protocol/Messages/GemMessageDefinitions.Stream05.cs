namespace Kwy.Communicate.Gem;

public static partial class GemMessageDefinitions
{
    public static SecsMessageDefinition AlarmReportSend { get; } =
        new(new SecsMessageId(5, 1), nameof(AlarmReportSend), true);
    public static SecsMessageDefinition EnableDisableAlarmSend { get; } =
        new(new SecsMessageId(5, 3), nameof(EnableDisableAlarmSend), true);
    public static SecsMessageDefinition EnableDisableAlarmAcknowledge { get; } =
        new(new SecsMessageId(5, 4), nameof(EnableDisableAlarmAcknowledge), false);
}
