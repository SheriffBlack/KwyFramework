
using Kwy.MVVM.Modularity;
using Kwy.MVVM.WPF.Mvvm;
using KwyPecvd.App.ViewModels;
using KwyPecvd.App.Views;
using Microsoft.Extensions.DependencyInjection;

namespace KwyPecvd.App;

public class AppModule : IModule
{
    public void OnInitialized(IServiceProvider provider)
    {
    }

    public void RegisterTypes(IServiceCollection services)
    {
        services.RegisterForNavigation<MainView, MainViewModel>();
        services.RegisterForNavigation<HomeView, HomeViewModel>();
    }
}
