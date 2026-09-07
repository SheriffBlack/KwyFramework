using Kwy.MVVM.Modularity;
using KwyTemplate.Security.Licensing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KwyTemplate.Security.SuperDog;

public sealed class SuperDogSecurityModule : IModule
{
    public void RegisterTypes(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<SuperDogOptions>();
        services.Replace(ServiceDescriptor.Singleton<ISecurityKeyChecker, SuperDogSecurityKeyChecker>());
    }

    public void OnInitialized(IServiceProvider provider)
    {
    }
}
