using Kwy.MVVM.Modularity;
using Kwy.MVVM.WPF;
using Kwy.MVVM.WPF.Mvvm;
using Kwy.UI.WPF.Components;
using KwyTemplate.App;
using KwyTemplate.Device;
using KwyTemplate.Flow;
using KwyTemplate.Vision;
using KwyTemplate.Security;
using KwyTemplate.Security.Options;
using KwyTemplate.Shell.Views;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace KwyTemplate.Shell;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : KwyApplication
{
    protected override Window CreateShell()
    {
        return CurrentServiceProvider!.Resolve<MainWindow>();
    }

    protected override void RegisterTypes(IServiceCollection services)
    {
        services.AddKwyWpfComponents();
        services.AddSingleton(new SecuritySessionOptions
        {
            ElevatedUserSessionDuration = TimeSpan.FromMinutes(1)
        });
    }

    protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
    {
        moduleCatalog.AddModule<SecurityModule>();
        moduleCatalog.AddModule<DeviceModule>();
        moduleCatalog.AddModule<FlowModule>();
        moduleCatalog.AddModule<AppModule>();
        moduleCatalog.AddModule<VisionModule>();
    }
}
