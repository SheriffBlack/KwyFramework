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
        services.TryAddSingleton<Machine_4_HAHH>();
        services.TryAddSingleton<MachineBase>(provider => provider.GetRequiredService<Machine_4_HAHH>());
        services.TryAddSingleton<IMachine>(provider => provider.GetRequiredService<Machine_4_HAHH>());
        services.TryAddSingleton<IMachineResultProvider>(provider => provider.GetRequiredService<Machine_4_HAHH>());
        services.TryAddSingleton<IStationOperationMachine>(provider => provider.GetRequiredService<Machine_4_HAHH>());
    }
    public void OnInitialized(IServiceProvider provider)
    {
    }
}







