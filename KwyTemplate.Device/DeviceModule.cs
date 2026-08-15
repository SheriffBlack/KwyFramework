using Kwy.Communicate.Abstractions;
using Kwy.Communicate.Core;
using Kwy.Communicate.NI;
using Kwy.Communicate.TcpSerial;
using Kwy.Device.Core;
using Kwy.Device.Abstractions;
using Kwy.MVVM.Modularity;
using KwyTemplate.Contracts.Modularity;
using KwyTemplate.Contracts.Services;
using KwyTemplate.Contracts.Localization;
using KwyTemplate.Device.Devices;
using KwyTemplate.Device.Profiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KwyTemplate.Device;

[Module(ModuleName = ModuleNames.DeviceModule)]
public sealed class DeviceModule : IModule
{
    public void RegisterTypes(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddKwyDeviceCore();
        services.TryAddSingleton<ICommunicationFactory>(_ =>
        {
            var factory = new CommunicationFactory();
            factory.RegisterTcpSerialClients();
            factory.RegisterGpib();
            return factory;
        });
        services.TryAddSingleton<DeviceConfigProvider>();
        services.TryAddSingleton<IDeviceConfigProvider>(provider => provider.GetRequiredService<DeviceConfigProvider>());
        services.TryAddSingleton<IMachineRuntimeOptionsProvider, MachineRuntimeOptionsProvider>();
        services.TryAddSingleton<IMachineProfileProvider, MachineProfileProvider>();
        services.TryAddSingleton<DeviceCatalogSelectionOptions>(provider => new DeviceCatalogSelectionOptions
        {
            ActiveCatalogKey = ResolveActiveCatalogKey(provider.GetRequiredService<IMachineRuntimeOptionsProvider>().Get())
        });
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDeviceCatalog, Machine_Default_DeviceCatalog>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDeviceCatalog, Machine_2_A_DeviceCatalog>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDeviceCatalog, Machine_4_HAHH_DeviceCatalog>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDeviceCatalog, ConfigurableDeviceCatalog>());

        services.TryAddSingleton<IMachineDeviceContext, MachineDeviceContext>();
        services.TryAddSingleton<IDeviceRegistryInitializer, DeviceRegistryInitializer>();
        services.TryAddSingleton<IDeviceStartupConnector, DeviceStartupConnector>();
    }

    public void OnInitialized(IServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ILocalizationService localizationService = provider.GetRequiredService<ILocalizationService>();
        string loadingMessage = localizationService.T("Startup.Device.LoadingConfiguration", "Loading device configuration...");
        provider.GetRequiredService<StartupProgressService>().Report(
            loadingMessage,
            10);
        provider.GetRequiredService<IDeviceRegistryInitializer>().Initialize();
        _ = provider.GetRequiredService<IDeviceStartupConnector>().ConnectAsync();
    }

    private static string ResolveActiveCatalogKey(MachineRuntimeOptions options)
        => string.Equals(options.ActiveMachineKey, MachineRuntimeOptions.ConfigurableMachineKey, StringComparison.OrdinalIgnoreCase)
            ? MachineRuntimeOptions.ConfigurableMachineKey
            : options.ActiveMachineKey switch
            {
                "Machine_2_A" => nameof(Machine_2_A_DeviceCatalog),
                "Machine_4_HAHH" => nameof(Machine_4_HAHH_DeviceCatalog),
                _ => nameof(Machine_Default_DeviceCatalog)
            };
}

