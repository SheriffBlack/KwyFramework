using System.Collections.Concurrent;
using Kwy.Device.Abstractions;

namespace Kwy.Device.Core;

public sealed class DeviceFactory : IDeviceFactory
{
    private readonly ConcurrentDictionary<Type, DeviceRegistration> registrations = new();

    public void Register<TConfig, TDevice>(Func<TConfig, TDevice> factory)
        where TConfig : IDeviceConfig
        where TDevice : IDevice
    {
        ArgumentNullException.ThrowIfNull(factory);

        registrations[typeof(TConfig)] = new DeviceRegistration(
            DeviceId: typeof(TDevice).Name,
            DeviceType: typeof(TDevice),
            ConfigType: typeof(TConfig),
            Factory: config => factory((TConfig)config));
    }

    public IDevice Create(IDeviceConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (!config.Validate())
        {
            throw new ArgumentException($"Invalid device config: {config.GetType().Name}", nameof(config));
        }

        if (!registrations.TryGetValue(config.GetType(), out var registration))
        {
            throw new InvalidOperationException($"No device factory registered for config type {config.GetType().FullName}.");
        }

        return registration.Factory(config);
    }

    public TDevice Create<TDevice>(IDeviceConfig config)
        where TDevice : IDevice
    {
        var device = Create(config);
        if (device is TDevice typed)
        {
            return typed;
        }

        throw new InvalidOperationException($"Factory created {device.GetType().FullName}, but {typeof(TDevice).FullName} was requested.");
    }

    public IReadOnlyCollection<DeviceRegistration> GetRegistrations()
    {
        return registrations.Values.ToArray();
    }
}
