using Kwy.Device.Core;
using Kwy.MVVM.Modularity;
using KwyTemplate.Contracts.Modularity;
using KwyTemplate.Device.Connections;
using KwyTemplate.Device.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KwyTemplate.Device;

[Module(ModuleName = ModuleNames.DeviceModule)]
public sealed class DeviceModule : IModule
{
    public void OnInitialized(IServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var startupService = provider.GetRequiredService<IDeviceStartupService>();
        _ = Task.Run(async () => await startupService.StartAsync().ConfigureAwait(false));
    }

    public void RegisterTypes(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddKwyDeviceCore();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDeviceConnectionFactory, HslPlcConnectionFactory>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDeviceConnectionFactory, ExternalTcpDeviceConnectionFactory>());
        services.TryAddSingleton<IDeviceConnectionOptionsStore, JsonDeviceConnectionOptionsStore>();
        services.TryAddSingleton<IDeviceConnectionService, DeviceConnectionService>();
        services.TryAddSingleton<IDeviceStartupService, DeviceStartupService>();
    }

}
