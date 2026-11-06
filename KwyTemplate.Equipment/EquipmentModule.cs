using Kwy.MVVM.Modularity;
using KwyTemplate.Contracts.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KwyTemplate.Equipment;

[Module(ModuleName = ModuleNames.EquipmentModule)]
[ModuleDependency(ModuleNames.ProcessModule)]
public sealed class EquipmentModule : IModule
{
    public void RegisterTypes(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IEquipmentCommandPolicy, EquipmentStateCommandPolicy>());
        services.TryAddSingleton<IEquipmentController, EquipmentController>();
    }

    public void OnInitialized(IServiceProvider provider)
    {
    }
}
