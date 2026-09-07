using Kwy.MVVM.Modularity;
using Kwy.MVVM.Core;
using Kwy.MVVM.WPF.Mvvm;
using Kwy.Data.EFCore.Sqlite;
using KwyTemplate.Security.Authentication;
using KwyTemplate.Security.Authorization;
using KwyTemplate.Security.Data;
using KwyTemplate.Security.Identity;
using KwyTemplate.Security.Licensing;
using KwyTemplate.Security.Options;
using KwyTemplate.Security.ViewModels;
using KwyTemplate.Security.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KwyTemplate.Security;

public sealed class SecurityModule : IModule
{
    public void RegisterTypes(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddKwyEfCoreSqlite<SecurityDbContext>(SecurityDataPaths.CreateConnectionString());
        services.TryAddSingleton<PasswordHasher>();
        services.TryAddSingleton<LocalUserStore>();
        services.TryAddSingleton<SecuritySessionOptions>();
        services.TryAddSingleton<ICurrentUserService, CurrentUserService>();
        services.TryAddSingleton<IPermissionService, SecurityPermissionService>();
        services.TryAddSingleton<ISecurityKeyChecker, NullSecurityKeyChecker>();
        services.TryAddSingleton<ILoginService, LocalLoginService>();
        services.TryAddSingleton<IAuthenticationDialogService, AuthenticationDialogService>();

        services.RegisterForNavigation<LoginView, LoginViewModel>();
    }

    public void OnInitialized(IServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        LocalUserStore userStore = provider.GetRequiredService<LocalUserStore>();
        _ = Task.Run(async () =>
        {
            try
            {
                await userStore.InitializeAsync().ConfigureAwait(false);
            }
            catch
            {
                // Login still performs a guarded initialization. Hook logging here when the template logger is finalized.
            }
        });
    }
}
