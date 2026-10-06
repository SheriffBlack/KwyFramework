using Kwy.MVVM.Core;
using Kwy.MVVM.Regions;
using KwyPecvd.Contracts.Navigation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KwyPecvd.App.ViewModels;

public class MainViewModel : BindableBase, INavigationAware
{
    private readonly IRegionManager regionManager;

    public MainViewModel(IRegionManager regionManager)
    {
        this.regionManager = regionManager;
    }

    public bool IsNavigationTarget(NavigationContext navigationContext) => true;

    public void OnNavigatedFrom(NavigationContext navigationContext)
    {
    }

    public void OnNavigatedTo(NavigationContext navigationContext)
    {
        regionManager.RequestNavigate(RegionNames.MainRegion, ViewNames.HomeView);
    }
}
