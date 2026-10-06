using Kwy.MVVM.Modularity;
using Kwy.MVVM.WPF;
using Kwy.MVVM.WPF.Mvvm;
using Kwy.UI.WPF.Components;
using KwyPecvd.App;
using KwyPecvd.Shell.Views;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace KwyPecvd.Shell;

public partial class App : KwyApplication
{
    protected override Window CreateShell()
    {
        IServiceProvider provider = CurrentServiceProvider ?? throw new InvalidOperationException("Service provider is not initialized.");
        return provider.Resolve<MainWindow>();
    }

    protected override void RegisterTypes(IServiceCollection services)
    {
        services.AddKwyWpfComponents();
    }

    protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
    {
        moduleCatalog.AddModule<AppModule>();
    }
}
