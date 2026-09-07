using Kwy.MVVM.Modularity;
using KwyTemplate.Contracts.Modularity;
using KwyTemplate.Flow.Machines;
using KwyTemplate.Flow.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KwyTemplate.Flow;

[Module(ModuleName = ModuleNames.FlowModule)]
[ModuleDependency(ModuleNames.DeviceModule)]
public sealed class FlowModule : IModule
{
    public void RegisterTypes(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IProductionRecordWriter, ProductionRecordWriter>();
        services.TryAddSingleton<Machine_2_A>();
        services.TryAddSingleton<Machine_4_HAHH>();
        services.TryAddSingleton<ConfigurableMachine>();
        services.TryAddSingleton<MachineBase>(provider => ResolveMachine(provider));
        services.TryAddSingleton<IMachine>(provider => provider.GetRequiredService<MachineBase>());
        services.TryAddSingleton<IMachineResultProvider>(provider => provider.GetRequiredService<MachineBase>());
        services.TryAddSingleton<IStationOperationMachine>(provider => provider.GetRequiredService<MachineBase>());
    }
    public void OnInitialized(IServiceProvider provider)
    {
    }

    private static MachineBase ResolveMachine(IServiceProvider provider)
    {
        var runtime = provider.GetRequiredService<KwyTemplate.Device.Profiles.IMachineRuntimeOptionsProvider>().Get();
        return string.Equals(runtime.ActiveMachineKey, KwyTemplate.Device.Profiles.MachineRuntimeOptions.ConfigurableMachineKey, StringComparison.OrdinalIgnoreCase)
            ? provider.GetRequiredService<ConfigurableMachine>()
            : string.Equals(runtime.ActiveMachineKey, "Machine_2_A", StringComparison.OrdinalIgnoreCase)
                ? provider.GetRequiredService<Machine_2_A>()
                : provider.GetRequiredService<Machine_4_HAHH>();
    }
}







