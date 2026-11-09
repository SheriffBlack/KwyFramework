namespace KwyPecvd.Equipment.Events;

/// <summary>
/// 发布 Equipment 业务事件。
/// </summary>
public interface IEquipmentEventPublisher
{
    Task PublishAsync(
        EquipmentEvent equipmentEvent,
        CancellationToken cancellationToken = default);
}
