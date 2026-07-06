namespace Kwy.Device.Abstractions;

public sealed record DeviceRegistration(
    string DeviceId,
    Type DeviceType,
    Type ConfigType,
    Func<IDeviceConfig, IDevice> Factory);
