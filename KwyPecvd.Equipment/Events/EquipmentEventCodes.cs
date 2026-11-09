namespace KwyPecvd.Equipment.Events;

/// <summary>
/// 定义 Equipment 对外发布的稳定事件代码。
/// 发生了什么业务事实
/// </summary>
public static class EquipmentEventCodes
{
    public const string ProcessStopped = "EQUIPMENT_EVENT_PROCESS_STOPPED";

    public const string ProcessStopFailed = "EQUIPMENT_EVENT_PROCESS_STOP_FAILED";
}
