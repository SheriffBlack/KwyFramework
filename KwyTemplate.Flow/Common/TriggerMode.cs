using System.ComponentModel;

namespace KwyTemplate.Flow.Common;

public enum TriggerMode
{
    [Description("硬触发，轮询模式")]
    Polling,

    [Description("硬触发，事件中断模式")]
    InterruptDriven,

    [Description("软触发")]
    Programmatic
}
