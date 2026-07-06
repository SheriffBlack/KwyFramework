using Kwy.Device.Abstractions;
using Kwy.Device.Abstractions.Equipment;

namespace Kwy.Device.Core.Equipment;

public sealed class EquipmentRecoveryOrchestrator : IEquipmentRecoveryOrchestrator
{
    private readonly IDeviceRegistry registry;
    private readonly IDeviceRecoveryService recoveryService;
    private readonly IEnumerable<IDeviceRecoveryParticipant> participants;
    private readonly IEquipmentStateMachine stateMachine;
    private readonly IEquipmentEventSink eventSink;

    public EquipmentRecoveryOrchestrator(
        IDeviceRegistry registry,
        IDeviceRecoveryService recoveryService,
        IEnumerable<IDeviceRecoveryParticipant> participants,
        IEquipmentStateMachine stateMachine,
        IEquipmentEventSink eventSink)
    {
        this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
        this.recoveryService = recoveryService ?? throw new ArgumentNullException(nameof(recoveryService));
        this.participants = participants ?? throw new ArgumentNullException(nameof(participants));
        this.stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
        this.eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
    }

    public async Task<EquipmentRecoveryOrchestrationResult> RecoverAsync(
        RecoveryPolicy policy,
        CancellationToken cancellationToken = default)
    {
        await stateMachine.TransitionAsync(EquipmentRunState.Recovering, "Equipment recovery started.", cancellationToken);

        var steps = new List<EquipmentRecoveryStepResult>();
        IDeviceRecoveryParticipant[] registeredParticipants = participants.ToArray();
        if (registeredParticipants.Length > 0)
        {
            foreach (IDeviceRecoveryParticipant participant in registeredParticipants)
            {
                cancellationToken.ThrowIfCancellationRequested();
                DeviceRecoveryResult result = await RecoverParticipantSafelyAsync(
                    participant.DeviceId,
                    () => participant.RecoverAsync(policy, cancellationToken),
                    cancellationToken);
                steps.Add(new EquipmentRecoveryStepResult(participant.DeviceId, result));
                await PublishRecoveryEventAsync(participant.DeviceId, result, cancellationToken);
            }
        }
        else
        {
            foreach (IDevice device in registry.Devices)
            {
                cancellationToken.ThrowIfCancellationRequested();
                DeviceRecoveryResult result = await RecoverParticipantSafelyAsync(
                    device.DeviceId,
                    () => recoveryService.RecoverAsync(
                        new DeviceRecoveryContext(device.DeviceId, policy),
                        cancellationToken),
                    cancellationToken);

                steps.Add(new EquipmentRecoveryStepResult(device.DeviceId, result));
                await PublishRecoveryEventAsync(device.DeviceId, result, cancellationToken);
            }
        }

        var orchestration = new EquipmentRecoveryOrchestrationResult(steps);
        await stateMachine.TransitionAsync(
            orchestration.IsRecovered ? EquipmentRunState.Idle : EquipmentRunState.ManualInterventionRequired,
            orchestration.IsRecovered ? "Equipment recovered to idle." : "Equipment recovery requires manual intervention.",
            cancellationToken);

        return orchestration;
    }

    private static async Task<DeviceRecoveryResult> RecoverParticipantSafelyAsync(
        string deviceId,
        Func<Task<DeviceRecoveryResult>> recover,
        CancellationToken cancellationToken)
    {
        try
        {
            return await recover();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new DeviceRecoveryResult(
                DeviceRecoveryState.Failed,
                Message: $"Device {deviceId} recovery failed: {ex.Message}");
        }
    }

    private Task PublishRecoveryEventAsync(
        string deviceId,
        DeviceRecoveryResult result,
        CancellationToken cancellationToken)
    {
        return eventSink.PublishAsync(new EquipmentEvent(
            "DeviceRecovery",
            $"Device {deviceId} recovery result: {result.State}.",
            result.IsRecovered ? EquipmentEventSeverity.Information : EquipmentEventSeverity.Warning,
            EquipmentEventKind.Recovery,
            deviceId), cancellationToken);
    }
}
