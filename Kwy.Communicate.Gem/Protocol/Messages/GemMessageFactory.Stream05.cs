using Secs4Net;
using static Secs4Net.Item;

namespace Kwy.Communicate.Gem;

public static partial class GemMessageFactory
{
    public static SecsMessage EnableDisableAlarmAcknowledge(bool accepted)
        => Create(GemMessageDefinitions.EnableDisableAlarmAcknowledge, B(accepted ? (byte)0 : (byte)1));

    public static SecsMessage AlarmReport(GemAlarm alarm)
    {
        var alarmCode = (byte)(alarm.AlarmCode & 0x7F);
        var alcd = alarm.State == GemAlarmState.Set
            ? (byte)(alarmCode | 0x80)
            : alarmCode;

        return Create(
            GemMessageDefinitions.AlarmReportSend,
            L(B(alcd), U4(alarm.AlarmId), A(alarm.Text)));
    }
}
