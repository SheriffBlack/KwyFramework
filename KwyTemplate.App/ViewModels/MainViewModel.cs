using Kwy.MVVM.Core;
using Kwy.MVVM.Regions;
using Kwy.MVVM.WPF.Regions;
using Kwy.UI;
using Kwy.UI.WPF.Components;
using KwyTemplate.App.Models;
using KwyTemplate.Contracts.Navigation;
using KwyTemplate.Contracts.Security;
using Kwy.UI.WPF.Components.Dialogs;

namespace KwyTemplate.App.ViewModels;

public class MainViewModel : BindableBase, INavigationAware
{
    private readonly IRegionManager regionManager;
    private readonly IPermissionService? permissionService;
    private readonly IDialogMessageService? dialogMessageService;

    public MainViewModel(
        IRegionManager regionManager,
        IPermissionService? permissionService = null,
        IDialogMessageService? dialogMessageService = null)
    {
        this.regionManager = regionManager;
        this.permissionService = permissionService;
        this.dialogMessageService = dialogMessageService;

        InitializeNavigationItems();
    }

    #region 导航集合

    /// <summary>
    /// 导航项集合
    /// 开发者只需在此处添加新的导航项配置，无需修改XAML
    /// </summary>
    public List<NavigationItemModel> NavigationItems { get; private set; } = new();

    /// <summary>
    /// 初始化导航项配置
    /// 开发者只需在此方法中添加或修改导航项配置
    /// </summary>
    private void InitializeNavigationItems()
    {
        NavigationItems = new List<NavigationItemModel>()
    {
        new NavigationItemModel
        {
            ViewName = ViewNames.HomeView,
            DisplayText = "主页",
            Icon = IconNames.IconHome,
        },
        //new NavigationItemModel
        //{
        //    ViewName = ViewNames.SetView,
        //    DisplayText = "设置",
        //    Icon =  IconNames.IconSet,
        //    PermissionCode = PermissionCodes.Engineer
        //} ,
        new NavigationItemModel
        {
            ViewName = ViewNames.SystemView,
            DisplayText = "系统",
            Icon =  IconNames.IconProcessing,
            PermissionCode = PermissionCodes.Engineer
        }
        };
    }

    #endregion 导航集合

    #region 初始化导航INavigationAware

    private bool isInitialized = false;

    /// <summary>
    /// 是否重用现有实例（返回 true 表示重用，不会重新创建 ViewModel）
    /// </summary>
    public bool IsNavigationTarget(NavigationContext navigationContext)
    {
        return true; // 重用现有实例
    }

    /// <summary>
    /// 导航离开时调用
    /// </summary>
    public void OnNavigatedFrom(NavigationContext navigationContext)
    {
        // 可以在这里保存状态
    }

    /// <summary>
    /// 导航到达时调用（首次导航和后续导航都会调用）
    /// </summary>
    public void OnNavigatedTo(NavigationContext navigationContext)
    {
        // 只在第一次导航时执行初始化
        if (!isInitialized)
        {
            isInitialized = true;
            SelectedView = ViewNames.HomeView;
            SelectedNavigationKey = ViewNames.HomeView;

            regionManager.RequestNavigate(RegionNames.MainRegion, ViewNames.HomeView);
        }
        else
        {
            RaisePropertyChanged(nameof(SelectedView));
        }
    }

    #endregion 初始化导航INavigationAware

    #region 导航 | 界面切换

    /// <summary>
    /// 当前选中的视图名称
    /// </summary>
    private string selectedView = string.Empty;

    public string SelectedView
    {
        get { return selectedView; }
        set { SetProperty(ref selectedView, value); }
    }

    private string selectedNavigationKey = string.Empty;

    public string SelectedNavigationKey
    {
        get { return selectedNavigationKey; }
        set { SetProperty(ref selectedNavigationKey, value); }
    }

    /// <summary>
    /// 导航命令
    /// 用于处理导航按钮的点击事件
    /// </summary>
    private DelegateCommand<NavigationItemModel>? navigateCommand;

    public DelegateCommand<NavigationItemModel> NavigateCommand => navigateCommand ??= new DelegateCommand<NavigationItemModel>(NavigateToView);

    /// <summary>
    /// 导航到指定视图
    /// </summary>
    /// <param name="item">要导航到的导航项。</param>
    private async void NavigateToView(NavigationItemModel? item)
    {
        if (item == null || string.IsNullOrEmpty(item.ViewName))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(item.PermissionCode)
            && permissionService?.HasPermission(item.PermissionCode) == false)
        {
            string message = permissionService.GetNoPermissionMessage(item.PermissionCode);
            if (dialogMessageService != null)
            {
                await dialogMessageService.ShowWarningAsync(message, "权限不足");
            }

            RaisePropertyChanged(nameof(SelectedNavigationKey));
            return;
        }

        SelectedView = item.ViewName;
        SelectedNavigationKey = item.NavigationKey;

        var parameters = new NavigationParameters();
        if (!string.IsNullOrWhiteSpace(item.Parameter))
        {
            parameters.Add(NavigationParameterKeys.Parameter, item.Parameter);
        }

        regionManager.RequestNavigate(RegionNames.MainRegion, item.ViewName, parameters);
    }

    #endregion 导航 | 界面切换
}
