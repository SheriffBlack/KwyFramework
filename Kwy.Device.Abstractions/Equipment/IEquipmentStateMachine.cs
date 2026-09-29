namespace Kwy.Device.Abstractions.Equipment;

/// <summary>
/// 定义整机运行状态机的只读公共接口。
/// </summary>
public interface IEquipmentStateMachine
{
    /// <summary>获取当前整机运行状态。</summary>
    EquipmentRunState CurrentState { get; }

    /// <summary>整机运行状态发生变化时触发。</summary>
    event EventHandler<EquipmentStateChangedEventArgs>? StateChanged;
}
