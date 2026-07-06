using Kwy.MVVM.Modularity;
using Kwy.MVVM.WPF.Mvvm;
using KwyTemplate.App.ViewModels;
using KwyTemplate.App.Views;
using KwyTemplate.Contracts.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace KwyTemplate.App;

[Module(ModuleName = ModuleNames.AppModule)]
public class AppModule : IModule
{
    public void OnInitialized(IServiceProvider containerProvider)
    {

    }

    public void RegisterTypes(IServiceCollection containerRegistry)
    {
        containerRegistry.RegisterForNavigation<MainView, MainViewModel>(); 
        containerRegistry.RegisterForNavigation<HomeView, HomeViewModel>(); 
        containerRegistry.RegisterForNavigation<SetView, SetViewModel>(); 
        containerRegistry.RegisterForNavigation<SystemView, SystemViewModel>(); 
    }
}
