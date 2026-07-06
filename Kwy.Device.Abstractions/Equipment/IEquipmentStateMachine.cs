namespace Kwy.Device.Abstractions.Equipment;

public interface IEquipmentStateMachine
{
    EquipmentRunState State { get; }

    event EventHandler<EquipmentStateChangedEventArgs>? StateChanged;

    EquipmentStateTransitionResult CanTransitionTo(EquipmentRunState targetState);

    Task TransitionAsync(
        EquipmentRunState targetState,
        string? reason = null,
        CancellationToken cancellationToken = default);

    Task ForceTransitionAsync(
        EquipmentRunState targetState,
        string? reason = null,
        CancellationToken cancellationToken = default);
}
