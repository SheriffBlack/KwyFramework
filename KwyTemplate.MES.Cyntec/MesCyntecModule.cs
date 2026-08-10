using Kwy.MVVM.Modularity;
using KwyTemplate.Contracts.Services;
using KwyTemplate.MES.Abstract.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KwyTemplate.MES.Cyntec;

public sealed class MesCyntecModule : IModule
{
    public void RegisterTypes(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<CyntecMesOptions>();
        services.TryAddSingleton<IProductionOutputOptions>(provider => provider.GetRequiredService<CyntecMesOptions>());
        services.TryAddSingleton<MesCyntecService>();
        services.TryAddSingleton<IMesConnection>(provider => provider.GetRequiredService<MesCyntecService>());
        services.TryAddSingleton<IMesReelService>(provider => provider.GetRequiredService<MesCyntecService>());
        services.TryAddSingleton<IMesTrackService>(provider => provider.GetRequiredService<MesCyntecService>());
        services.TryAddSingleton<IMesWorkOrderService>(provider => provider.GetRequiredService<MesCyntecService>());
        services.TryAddSingleton<IMesStandardSampleService>(provider => provider.GetRequiredService<MesCyntecService>());
    }

    public void OnInitialized(IServiceProvider provider)
    {
    }
}

