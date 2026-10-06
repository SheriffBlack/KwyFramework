using Kwy.MVVM.Core;
using Kwy.MVVM.Regions;
using KwyPecvd.Contracts.Navigation;

namespace KwyPecvd.Shell.ViewModels;

public class MainWindowViewModel: BindableBase
{
    private readonly IRegionManager regionManager;

    public MainWindowViewModel(IRegionManager regionManager)
    {
        this.regionManager = regionManager;

    }


    private DelegateCommand? loadCommand;
    public DelegateCommand LoadCommand => loadCommand ??= new DelegateCommand(ExcuteLoadCommand);

    private void ExcuteLoadCommand()
    {
        regionManager.RequestNavigate(RegionNames.WindowRegion,ViewNames.MainView);
    }
}
