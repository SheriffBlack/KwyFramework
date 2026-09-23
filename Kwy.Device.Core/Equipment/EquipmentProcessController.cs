using Kwy.Device.Abstractions.Equipment;
using Kwy.Device.Abstractions.Motion;

namespace Kwy.Device.Core.Equipment;

public sealed class EquipmentProcessController : IEquipmentProcessController
{
    private readonly IEquipmentStateMachine stateMachine;
    private readonly IDeviceStateSynchronizer stateSynchronizer;
    private readonly IDeviceSafetyGuard safetyGuard;
    private readonly IEquipmentEventSink eventSink;
    private readonly IReadOnlyList<IMotionAutoModeGate> motionAutoModeGates;
    private readonly IReadOnlyList<IMotionDeviceRuntime> motionRuntimes;

    public EquipmentProcessController(
        IEquipmentStateMachine stateMachine,
        IDeviceStateSynchronizer stateSynchronizer,
        IDeviceSafetyGuard safetyGuard,
        IEquipmentEventSink eventSink,
        IEnumerable<IMotionAutoModeGate> motionAutoModeGates,
        IEnumerable<IMotionDeviceRuntime> motionRuntimes)
    {
        this.stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
        this.stateSynchronizer = stateSynchronizer ?? throw new ArgumentNullException(nameof(stateSynchronizer));
        this.safetyGuard = safetyGuard ?? throw new ArgumentNullException(nameof(safetyGuard));
        this.eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
        this.motionAutoModeGates = motionAutoModeGates?.ToArray()
            ?? throw new ArgumentNullException(nameof(motionAutoModeGates));
        this.motionRuntimes = motionRuntimes?.ToArray()
            ?? throw new ArgumentNullException(nameof(motionRuntimes));
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

        EquipmentOperationResult? motionGateFailure = await EnsureMotionReadyAsync(cancellationToken);
        if (motionGateFailure is not null)
        {
            return motionGateFailure;
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

        EquipmentOperationResult? motionGateFailure = await EnsureMotionReadyAsync(cancellationToken);
        if (motionGateFailure is not null)
        {
            return motionGateFailure;
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

        EquipmentOperationResult? motionGateFailure = await EnsureMotionReadyAsync(cancellationToken);
        if (motionGateFailure is not null)
        {
            return motionGateFailure;
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

    /// <summary>连接状态与常规设备检查完成后，再执行运动配置硬门禁。</summary>
    private async Task<EquipmentOperationResult?> EnsureMotionReadyAsync(CancellationToken cancellationToken)
    {
        try
        {
            foreach (IMotionDeviceRuntime runtime in motionRuntimes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!runtime.Card.IsConnected)
                {
                    throw new InvalidOperationException($"Motion controller '{runtime.DeviceId}' is not connected.");
                }

                // StartAsync 在返回前已经完成首帧采集，随后门禁可安全读取轴快照。
                await runtime.StateMonitor.StartAsync(cancellationToken).ConfigureAwait(false);
            }

            foreach (IMotionAutoModeGate gate in motionAutoModeGates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                gate.EnsureReadyForAutoMode();
            }

            return null;
        }
        catch (Exception exception) when (exception is MotionConfigurationException or InvalidOperationException)
        {
            const string reason = "Motion configuration validation failed.";
            await stateMachine.ForceTransitionAsync(EquipmentRunState.ManualInterventionRequired, reason, cancellationToken);
            return new EquipmentOperationResult(false, stateMachine.State, exception.Message);
        }
    }
}
