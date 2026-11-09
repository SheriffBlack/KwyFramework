using KwyPecvd.Equipment.Events;
using KwyPecvd.Process.Chamber;
using KwyPecvd.Process.Modules;

namespace KwyPecvd.Equipment.Commands;

/// <summary>
/// 根据 Process 层的最终状态，完成已经接受的异步命令。
/// </summary>
public sealed class EquipmentCommandCompletionObserver : IEquipmentCommandCompletionObserver
{
    private readonly IPendingEquipmentCommandStore pendingCommandStore;
    private readonly IEquipmentEventPublisher eventPublisher;

    public EquipmentCommandCompletionObserver(
        IPendingEquipmentCommandStore pendingCommandStore,
        IEquipmentEventPublisher eventPublisher)
    {
        ArgumentNullException.ThrowIfNull(pendingCommandStore);
        ArgumentNullException.ThrowIfNull(eventPublisher);

        this.pendingCommandStore = pendingCommandStore;
        this.eventPublisher = eventPublisher;
    }

    public Task ObserveAsync(IEquipmentModuleController module, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(module);

        return module switch
        {
            IChamberController chamber =>
                ObserveChamberAsync(
                    chamber,
                    cancellationToken),

            _ => Task.CompletedTask
        };
    }

    private Task ObserveChamberAsync(
        IChamberController chamber,
        CancellationToken cancellationToken)
    {
        var snapshot = chamber.Snapshot;

        return snapshot.State switch
        {
            ChamberState.Idle =>
                CompleteStopAsync(
                    chamber.Id,
                    EquipmentCommandStatus.Completed,
                    EquipmentCommandResultCodes.Completed,
                    EquipmentEventCodes.ProcessStopped,
                    $"Chamber '{chamber.Id}' process stopped.",
                    cancellationToken),

            ChamberState.Faulted =>
                CompleteStopAsync(
                    chamber.Id,
                    EquipmentCommandStatus.Failed,
                    snapshot.FaultCode ??
                        EquipmentCommandResultCodes.ExecutionFailed,
                    EquipmentEventCodes.ProcessStopFailed,
                    snapshot.FaultMessage ??
                        $"Chamber '{chamber.Id}' process stop failed.",
                    cancellationToken),

            // Offline、Initializing、Preparing、Processing、Stopping
            // 都不是本次异步 Stop 命令的最终完成状态。
            _ => Task.CompletedTask
        };
    }

    private async Task CompleteStopAsync(
        string equipmentModuleId,
        EquipmentCommandStatus status,
        string resultCode,
        string eventCode,
        string message,
        CancellationToken cancellationToken)
    {
        if (!pendingCommandStore.TryTake(
                equipmentModuleId,
                EquipmentCommandTypeCodes.StopProcess,
                out var pending))
        {
            return;
        }

        try
        {
            var equipmentEvent =
                new EquipmentEvent(
                    EventId: Guid.NewGuid().ToString("N"),
                    Code: eventCode,
                    OccurredAt: DateTimeOffset.UtcNow,
                    EquipmentModuleId: equipmentModuleId,
                    CommandContext: pending!.Context,
                    CommandStatus: status,
                    ResultCode: resultCode,
                    Message: message);

            await eventPublisher.PublishAsync(equipmentEvent, cancellationToken);
        }
        catch
        {
            // 发布失败时恢复 Pending，允许下个 RT 周期重试。
            pendingCommandStore.TryAdd(pending!);
            throw;
        }
    }
}
