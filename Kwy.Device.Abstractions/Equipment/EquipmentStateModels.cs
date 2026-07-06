namespace Kwy.Device.Abstractions.Equipment;

public enum EquipmentRunState
{
    Unknown,
    Idle,
    Initializing,
    Ready,
    Running,
    Pausing,
    Paused,
    Resuming,
    Stopping,
    Stopped,
    Recovering,
    Alarm,
    Error,
    Manual,
    Maintenance,
    ManualInterventionRequired
}

public sealed record EquipmentStateChangedEventArgs(
    EquipmentRunState PreviousState,
    EquipmentRunState CurrentState,
    string? Reason = null);

public sealed record EquipmentStateTransitionResult(
    bool IsAllowed,
    EquipmentRunState From,
    EquipmentRunState To,
    string? Reason = null)
{
    public static EquipmentStateTransitionResult Allowed(EquipmentRunState from, EquipmentRunState to)
        => new(true, from, to);

    public static EquipmentStateTransitionResult Denied(EquipmentRunState from, EquipmentRunState to, string reason)
        => new(false, from, to, reason);
}
