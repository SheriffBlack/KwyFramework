using KwyPecvd.Equipment.Events;

namespace KwyPecvd.RT.Runtime;

/// <summary>
/// 当前阶段将 Equipment Event 写入日志。
/// 后续可以替换或组合为 GEM、gRPC、消息队列发布器。
/// </summary>
public sealed class LoggingEquipmentEventPublisher(ILogger<LoggingEquipmentEventPublisher> logger) : IEquipmentEventPublisher
{
    public Task PublishAsync(EquipmentEvent equipmentEvent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        logger.LogInformation(
            "Equipment event {EventCode}: " +
            "Module={ModuleId}, RequestId={RequestId}, " +
            "CommandStatus={CommandStatus}, ResultCode={ResultCode}, " +
            "Message={Message}",
            equipmentEvent.Code,
            equipmentEvent.EquipmentModuleId,
            equipmentEvent.CommandContext?.RequestId,
            equipmentEvent.CommandStatus,
            equipmentEvent.ResultCode,
            equipmentEvent.Message);

        return Task.CompletedTask;
    }
}
