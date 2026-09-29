namespace Kwy.Device.Abstractions.Equipment;

/// <summary>
/// 定义整机事件的异步接收入口。
/// </summary>
public interface IEquipmentEventSink
{
    /// <summary>
    /// 异步发布整机事件。
    /// </summary>
    /// <param name="equipmentEvent">要发布的整机事件。</param>
    /// <param name="cancellationToken">用于取消操作的令牌。</param>
    Task PublishAsync(EquipmentEvent equipmentEvent, CancellationToken cancellationToken = default);
}
