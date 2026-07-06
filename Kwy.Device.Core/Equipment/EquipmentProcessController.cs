using Kwy.Device.Abstractions.Equipment;

namespace Kwy.Device.Core.Equipment;

public sealed class EquipmentProcessController : IEquipmentProcessController
{
    private readonly IEquipmentStateMachine stateMachine;
    private readonly IDeviceStateSynchronizer stateSynchronizer;
    private readonly IDeviceSafetyGuard safetyGuard;
    private readonly IEquipmentEventSink eventSink;

    public EquipmentProcessController(
        IEquipmentStateMachine stateMachine,
        IDeviceStateSynchronizer stateSynchronizer,
        IDeviceSafetyGuard safetyGuard,
        IEquipmentEventSink eventSink)
    {
        this.stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
        this.stateSynchronizer = stateSynchronizer ?? throw new ArgumentNullException(nameof(stateSynchronizer));
        this.safetyGuard = safetyGuard ?? throw new ArgumentNullException(nameof(safetyGuard));
        this.eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
    }

    public async Task<EquipmentOperationResult> InitializeAsync(CancellationToken cancellationToken = default)
    {
        await stateMachine.TransitionAsync(EquipmentRunState.Initializing, "Initialize requested.", cancellationToken);

        DeviceSyncResult sync = await stateSynchronizer.SyncStateAsync(cancellationToken);
        if (!sync.IsReady)
        {
            await stateMachine.ForceTransitionAsync(EquipmentRunState.ManualInterventionRequired, sync.Message, cancellationToken);
            return new EquipmentOperationResult(false, stateMachine.State, sync.Message);
        }

        DeviceSafetyResult safety = await safetyGuard.CheckAsync(cancellationToken);
        if (!safety.IsAllowed)
        {
            await stateMachine.ForceTransitionAsync(EquipmentRunState.ManualInterventionRequired, "Safety check failed.", cancellationToken);
            return new EquipmentOperationResult(false, stateMachine.State, string.Join("; ", safety.Violations.Select(item => item.Message)));
        }

        await stateMachine.TransitionAsync(EquipmentRunState.Ready, "Initialize completed.", cancellationToken);
        await PublishAsync("Initialize", "Equipment initialized.", cancellationToken);
        return new EquipmentOperationResult(true, stateMachine.State);
    }

    public async Task<EquipmentOperationResult> StartAsync(CancellationToken cancellationToken = default)
    {
        DeviceSyncResult sync = await stateSynchronizer.SyncStateAsync(cancellationToken);
        if (!sync.IsReady)
        {
            await stateMachine.ForceTransitionAsync(EquipmentRunState.ManualInterventionRequired, sync.Message, cancellationToken);
            return new EquipmentOperationResult(false, stateMachine.State, sync.Message);
        }

        DeviceSafetyResult safety = await safetyGuard.CheckAsync(cancellationToken);
        if (!safety.IsAllowed)
        {
            await stateMachine.ForceTransitionAsync(EquipmentRunState.ManualInterventionRequired, "Safety check failed.", cancellationToken);
            return new EquipmentOperationResult(false, stateMachine.State, string.Join("; ", safety.Violations.Select(item => item.Message)));
        }

        await stateMachine.TransitionAsync(EquipmentRunState.Running, "Start requested.", cancellationToken);
        await PublishAsync("Start", "Equipment started.", cancellationToken);
        return new EquipmentOperationResult(true, stateMachine.State);
    }

    public async Task<EquipmentOperationResult> PauseAsync(CancellationToken cancellationToken = default)
    {
        await stateMachine.TransitionAsync(EquipmentRunState.Pausing, "Pause requested.", cancellationToken);
        await stateMachine.TransitionAsync(EquipmentRunState.Paused, "Equipment paused.", cancellationToken);
        await PublishAsync("Pause", "Equipment paused.", cancellationToken);
        return new EquipmentOperationResult(true, stateMachine.State);
    }

    public async Task<EquipmentOperationResult> ResumeAsync(CancellationToken cancellationToken = default)
    {
        DeviceSyncResult sync = await stateSynchronizer.SyncStateAsync(cancellationToken);
        if (!sync.IsReady)
        {
            await stateMachine.ForceTransitionAsync(EquipmentRunState.ManualInterventionRequired, sync.Message, cancellationToken);
            return new EquipmentOperationResult(false, stateMachine.State, sync.Message);
        }

        DeviceSafetyResult safety = await safetyGuard.CheckAsync(cancellationToken);
        if (!safety.IsAllowed)
        {
            await stateMachine.ForceTransitionAsync(EquipmentRunState.ManualInterventionRequired, "Safety check failed.", cancellationToken);
            return new EquipmentOperationResult(false, stateMachine.State, string.Join("; ", safety.Violations.Select(item => item.Message)));
        }

        await stateMachine.TransitionAsync(EquipmentRunState.Resuming, "Resume requested.", cancellationToken);
        await stateMachine.TransitionAsync(EquipmentRunState.Running, "Equipment resumed.", cancellationToken);
        await PublishAsync("Resume", "Equipment resumed.", cancellationToken);
        return new EquipmentOperationResult(true, stateMachine.State);
    }

    public async Task<EquipmentOperationResult> StopAsync(CancellationToken cancellationToken = default)
    {
        await stateMachine.TransitionAsync(EquipmentRunState.Stopping, "Stop requested.", cancellationToken);
        await stateMachine.TransitionAsync(EquipmentRunState.Stopped, "Equipment stopped.", cancellationToken);
        await PublishAsync("Stop", "Equipment stopped.", cancellationToken);
        return new EquipmentOperationResult(true, stateMachine.State);
    }

    public async Task<EquipmentOperationResult> AbortAsync(CancellationToken cancellationToken = default)
    {
        await stateMachine.ForceTransitionAsync(EquipmentRunState.Error, "Abort requested.", cancellationToken);
        await PublishAsync("Abort", "Equipment aborted.", cancellationToken);
        return new EquipmentOperationResult(true, stateMachine.State);
    }

    public async Task<EquipmentOperationResult> ClearAsync(CancellationToken cancellationToken = default)
    {
        await stateMachine.ForceTransitionAsync(EquipmentRunState.Recovering, "Clear requested.", cancellationToken);

        DeviceSyncResult sync = await stateSynchronizer.SyncStateAsync(cancellationToken);
        if (!sync.IsReady)
        {
            await stateMachine.TransitionAsync(EquipmentRunState.ManualInterventionRequired, sync.Message, cancellationToken);
            return new EquipmentOperationResult(false, stateMachine.State, sync.Message);
        }

        DeviceSafetyResult safety = await safetyGuard.CheckAsync(cancellationToken);
        if (!safety.IsAllowed)
        {
            await stateMachine.TransitionAsync(EquipmentRunState.ManualInterventionRequired, "Safety check failed.", cancellationToken);
            return new EquipmentOperationResult(false, stateMachine.State, string.Join("; ", safety.Violations.Select(item => item.Message)));
        }

        await stateMachine.TransitionAsync(EquipmentRunState.Idle, "Equipment cleared.", cancellationToken);
        await PublishAsync("Clear", "Equipment cleared.", cancellationToken);
        return new EquipmentOperationResult(true, stateMachine.State);
    }

    private Task PublishAsync(string code, string message, CancellationToken cancellationToken)
        => eventSink.PublishAsync(new EquipmentEvent(code, message, EquipmentEventSeverity.Information, EquipmentEventKind.Operation), cancellationToken);
}
