using Kwy.MVVM.Modularity;
using KwyTemplate.Contracts.Modularity;
using KwyTemplate.Flow.DeviceProfiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KwyTemplate.Flow;

[Module(ModuleName = ModuleNames.FlowModule)]
public sealed class FlowModule : IModule
{
    public void RegisterTypes(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IMachineDeviceResolver, MachineDeviceResolver>();
    }

    public void OnInitialized(IServiceProvider provider)
    {
    }
}
