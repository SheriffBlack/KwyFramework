namespace Kwy.Device.Abstractions.Equipment;

public enum RecoveryPolicy
{
    ManualOnly,
    AutoReconnectOnly,
    AutoRecoverToIdle,
    AutoResumeWhenSafe
}

public enum DeviceRecoveryState
{
    NotStarted,
    Reconnected,
    StateSynchronized,
    SafetyChecked,
    RecoveredToIdle,
    ResumeAllowed,
    ManualInterventionRequired,
    Failed
}

public sealed record DeviceRecoveryContext(
    string DeviceId,
    RecoveryPolicy Policy,
    Exception? Failure = null);

public sealed record DeviceRecoveryResult(
    DeviceRecoveryState State,
    DeviceSyncResult? SyncResult = null,
    DeviceSafetyResult? SafetyResult = null,
    string? Message = null)
{
    public bool IsRecovered => State is DeviceRecoveryState.RecoveredToIdle or DeviceRecoveryState.ResumeAllowed;
}
