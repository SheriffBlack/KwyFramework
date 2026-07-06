namespace Kwy.Device.Abstractions.Equipment;

/// <summary>
/// Device-specific state synchronization contribution used by the global equipment synchronizer.
/// </summary>
public interface IDeviceStateParticipant : IDeviceStateSynchronizer
{
    string DeviceId { get; }
}

/// <summary>
/// Device-specific safety contribution used by the global equipment safety guard.
/// </summary>
public interface IDeviceSafetyParticipant : IDeviceSafetyGuard
{
    string DeviceId { get; }
}
