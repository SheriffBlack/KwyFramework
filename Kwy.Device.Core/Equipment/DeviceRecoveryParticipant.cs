using Kwy.Device.Abstractions.Equipment;

namespace Kwy.Device.Core.Equipment;

public sealed class DeviceRecoveryParticipant : IDeviceRecoveryParticipant
{
    private readonly IDeviceStateSynchronizer stateSynchronizer;
    private readonly IDeviceSafetyGuard safetyGuard;

    public DeviceRecoveryParticipant(
        string deviceId,
        IDeviceStateSynchronizer stateSynchronizer,
        IDeviceSafetyGuard safetyGuard)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        DeviceId = deviceId;
        this.stateSynchronizer = stateSynchronizer ?? throw new ArgumentNullException(nameof(stateSynchronizer));
        this.safetyGuard = safetyGuard ?? throw new ArgumentNullException(nameof(safetyGuard));
    }

    public string DeviceId { get; }

    public async Task<DeviceRecoveryResult> RecoverAsync(
        RecoveryPolicy policy,
        CancellationToken cancellationToken = default)
    {
        return policy switch
        {
            RecoveryPolicy.ManualOnly => new DeviceRecoveryResult(
                DeviceRecoveryState.ManualInterventionRequired,
                Message: "Manual confirmation is required by recovery policy."),
            RecoveryPolicy.AutoReconnectOnly => new DeviceRecoveryResult(
                DeviceRecoveryState.Reconnected,
                Message: "Communication reconnection is complete. Device state has not been synchronized."),
            RecoveryPolicy.AutoRecoverToIdle => await RecoverToIdleAsync(cancellationToken),
            RecoveryPolicy.AutoResumeWhenSafe => await RecoverAndAllowResumeAsync(cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(policy), policy, "Unsupported recovery policy.")
        };
    }

    private async Task<DeviceRecoveryResult> RecoverToIdleAsync(CancellationToken cancellationToken)
    {
        DeviceSyncResult syncResult = await stateSynchronizer.SyncStateAsync(cancellationToken);
        if (!syncResult.IsReady)
        {
            return new DeviceRecoveryResult(DeviceRecoveryState.Failed, syncResult, Message: syncResult.Message);
        }

        DeviceSafetyResult safetyResult = await safetyGuard.CheckAsync(cancellationToken);
        return safetyResult.IsAllowed
            ? new DeviceRecoveryResult(DeviceRecoveryState.RecoveredToIdle, syncResult, safetyResult)
            : new DeviceRecoveryResult(DeviceRecoveryState.ManualInterventionRequired, syncResult, safetyResult);
    }

    private async Task<DeviceRecoveryResult> RecoverAndAllowResumeAsync(CancellationToken cancellationToken)
    {
        DeviceRecoveryResult result = await RecoverToIdleAsync(cancellationToken);
        return result.State == DeviceRecoveryState.RecoveredToIdle
            ? result with { State = DeviceRecoveryState.ResumeAllowed }
            : result;
    }
}
