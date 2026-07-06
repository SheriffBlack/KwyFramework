using Kwy.Device.Abstractions.Equipment;

namespace Kwy.Device.Core.Equipment;

public sealed class EquipmentStateMachine : IEquipmentStateMachine
{
    private readonly SemaphoreSlim transitionSemaphore = new(1, 1);
    private static readonly IReadOnlyDictionary<EquipmentRunState, EquipmentRunState[]> AllowedTransitions =
        new Dictionary<EquipmentRunState, EquipmentRunState[]>
        {
            [EquipmentRunState.Unknown] = new[] { EquipmentRunState.Initializing, EquipmentRunState.Idle, EquipmentRunState.Error },
            [EquipmentRunState.Idle] = new[] { EquipmentRunState.Initializing, EquipmentRunState.Ready, EquipmentRunState.Manual, EquipmentRunState.Maintenance, EquipmentRunState.Recovering, EquipmentRunState.Error },
            [EquipmentRunState.Initializing] = new[] { EquipmentRunState.Ready, EquipmentRunState.Idle, EquipmentRunState.Error, EquipmentRunState.ManualInterventionRequired },
            [EquipmentRunState.Ready] = new[] { EquipmentRunState.Running, EquipmentRunState.Manual, EquipmentRunState.Maintenance, EquipmentRunState.Stopping, EquipmentRunState.Recovering, EquipmentRunState.Alarm, EquipmentRunState.Error },
            [EquipmentRunState.Running] = new[] { EquipmentRunState.Pausing, EquipmentRunState.Stopping, EquipmentRunState.Alarm, EquipmentRunState.Error },
            [EquipmentRunState.Pausing] = new[] { EquipmentRunState.Paused, EquipmentRunState.Stopping, EquipmentRunState.Error },
            [EquipmentRunState.Paused] = new[] { EquipmentRunState.Resuming, EquipmentRunState.Stopping, EquipmentRunState.Recovering, EquipmentRunState.Error },
            [EquipmentRunState.Resuming] = new[] { EquipmentRunState.Running, EquipmentRunState.Paused, EquipmentRunState.Error },
            [EquipmentRunState.Stopping] = new[] { EquipmentRunState.Stopped, EquipmentRunState.Idle, EquipmentRunState.Error },
            [EquipmentRunState.Stopped] = new[] { EquipmentRunState.Idle, EquipmentRunState.Recovering, EquipmentRunState.Manual, EquipmentRunState.Error },
            [EquipmentRunState.Recovering] = new[] { EquipmentRunState.Idle, EquipmentRunState.Ready, EquipmentRunState.ManualInterventionRequired, EquipmentRunState.Error },
            [EquipmentRunState.Alarm] = new[] { EquipmentRunState.Recovering, EquipmentRunState.ManualInterventionRequired, EquipmentRunState.Error },
            [EquipmentRunState.Error] = new[] { EquipmentRunState.Recovering, EquipmentRunState.ManualInterventionRequired, EquipmentRunState.Maintenance },
            [EquipmentRunState.Manual] = new[] { EquipmentRunState.Idle, EquipmentRunState.Ready, EquipmentRunState.Maintenance, EquipmentRunState.Error },
            [EquipmentRunState.Maintenance] = new[] { EquipmentRunState.Idle, EquipmentRunState.Manual, EquipmentRunState.Error },
            [EquipmentRunState.ManualInterventionRequired] = new[] { EquipmentRunState.Recovering, EquipmentRunState.Manual, EquipmentRunState.Maintenance }
        };

    public EquipmentRunState State { get; private set; } = EquipmentRunState.Idle;

    public event EventHandler<EquipmentStateChangedEventArgs>? StateChanged;

    public EquipmentStateTransitionResult CanTransitionTo(EquipmentRunState targetState)
    {
        if (State == targetState)
        {
            return EquipmentStateTransitionResult.Allowed(State, targetState);
        }

        if (AllowedTransitions.TryGetValue(State, out var targets) && targets.Contains(targetState))
        {
            return EquipmentStateTransitionResult.Allowed(State, targetState);
        }

        return EquipmentStateTransitionResult.Denied(State, targetState, $"Transition from {State} to {targetState} is not allowed.");
    }

    public async Task TransitionAsync(
        EquipmentRunState targetState,
        string? reason = null,
        CancellationToken cancellationToken = default)
        => await TransitionCoreAsync(targetState, reason, allowForced: false, cancellationToken);

    public async Task ForceTransitionAsync(
        EquipmentRunState targetState,
        string? reason = null,
        CancellationToken cancellationToken = default)
        => await TransitionCoreAsync(targetState, reason, allowForced: true, cancellationToken);

    private async Task TransitionCoreAsync(
        EquipmentRunState targetState,
        string? reason,
        bool allowForced,
        CancellationToken cancellationToken)
    {
        await transitionSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (State == targetState)
            {
                return;
            }

            EquipmentStateTransitionResult transition = CanTransitionTo(targetState);
            if (!allowForced && !transition.IsAllowed)
            {
                throw new InvalidOperationException(transition.Reason);
            }

            var previous = State;
            State = targetState;
            StateChanged?.Invoke(this, new EquipmentStateChangedEventArgs(previous, targetState, reason));
        }
        finally
        {
            transitionSemaphore.Release();
        }
    }
}
