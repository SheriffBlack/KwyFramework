using Kwy.Device.Abstractions;
using Kwy.Device.Abstractions.Equipment;
using Kwy.Device.Abstractions.PLC;
using Kwy.Device.Core;
using Kwy.Device.Core.Equipment;
using Kwy.Device.PLCs.Hsl.Licensing;
using Kwy.Licensing.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kwy.Device.PLCs.Hsl;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHslCommunicationLicense(
        this IServiceCollection services,
        Action<HslCommunicationLicenseOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new HslCommunicationLicenseOptions();
        configure(options);

        services.TryAddSingleton<ILicenseActivationService, LicenseActivationService>();
        services.AddSingleton(options);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ILicenseActivator, HslCommunicationLicenseActivator>());

        return services;
    }

    public static IServiceCollection AddKwyHslPlc(
        this IServiceCollection services,
        string deviceId,
        string deviceName,
        Action<HslPlcConfig> configure,
        Action<HslPlcRuntimeOptions>? configureRuntime = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceName);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddKwyDeviceCore();

        var config = new HslPlcConfig();
        configure(config);
        if (!config.Validate())
        {
            throw new ArgumentException("Invalid HSL PLC configuration.", nameof(configure));
        }

        var runtimeOptions = new HslPlcRuntimeOptions();
        configureRuntime?.Invoke(runtimeOptions);

        var device = new Lazy<HslPlcDevice>(() => new HslPlcDevice(deviceId, deviceName, config));
        var stateSynchronizer = new Lazy<HslPlcStateSynchronizer>(() => new HslPlcStateSynchronizer(device.Value, runtimeOptions));
        var safetyGuard = new Lazy<HslPlcSafetyGuard>(() => new HslPlcSafetyGuard(device.Value, runtimeOptions));

        services.AddSingleton(provider =>
        {
            var plc = device.Value;
            provider.GetRequiredService<IDeviceRegistry>().AddOrUpdate(plc);
            return plc;
        });
        services.AddSingleton<IPlcDevice>(provider => provider.GetRequiredService<HslPlcDevice>());
        services.AddSingleton<IDeviceStateParticipant>(_ => stateSynchronizer.Value);
        services.AddSingleton<IDeviceSafetyParticipant>(_ => safetyGuard.Value);
        services.AddSingleton<IDeviceRecoveryParticipant>(_ => new DeviceRecoveryParticipant(
            deviceId,
            stateSynchronizer.Value,
            safetyGuard.Value));

        return services;
    }
}
