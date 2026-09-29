namespace Kwy.Device.Abstractions.Equipment;

/// <summary>
/// 提供整机运行状态发生变化时的数据。
/// </summary>
public sealed class EquipmentStateChangedEventArgs : EventArgs
{
    /// <summary>
    /// 初始化整机状态变化事件参数。
    /// </summary>
    /// <param name="previousState">变化前的运行状态。</param>
    /// <param name="currentState">变化后的运行状态。</param>
    /// <param name="reason">状态变化原因，可为空。</param>
    /// <param name="timestamp">状态变化时间；为空时使用当前本地时间。</param>
    public EquipmentStateChangedEventArgs(
        EquipmentRunState previousState,
        EquipmentRunState currentState,
        string? reason = null,
        DateTimeOffset? timestamp = null)
    {
        PreviousState = previousState;
        CurrentState = currentState;
        Reason = reason;
        Timestamp = timestamp ?? DateTimeOffset.Now;
    }

    /// <summary>获取变化前的运行状态。</summary>
    public EquipmentRunState PreviousState { get; }

    /// <summary>获取变化后的运行状态。</summary>
    public EquipmentRunState CurrentState { get; }

    /// <summary>获取状态变化原因。</summary>
    public string? Reason { get; }

    /// <summary>获取状态变化时间。</summary>
    public DateTimeOffset Timestamp { get; }
}
