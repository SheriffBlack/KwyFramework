using Kwy.UI;
using KwyTemplate.Contracts.Navigation;
using KwyTemplate.Contracts.Security;

namespace KwyTemplate.App.Models;

/// <summary>
/// 模板导航数据集合。
/// </summary>
public sealed class NavigationConfigModel
{
    /// <summary>
    /// MainView 顶部一级导航。
    /// </summary>
    public List<NavigationItemModel> PrimaryNavigationItems { get; set; } =
    [
        new NavigationItemModel
        {
            ViewName = ViewNames.HomeView,
            DisplayText = "主页",
            LocalizationKey = "Nav.Home",
            Icon = IconNames.IconHome
        },
        new NavigationItemModel
        {
            ViewName = ViewNames.CompensateView,
            DisplayText = "点检",
            LocalizationKey = "Nav.Compensate",
            Icon = IconNames.IconCompensate
        },
        new NavigationItemModel
        {
            ViewName = ViewNames.StationView,
            DisplayText = "手动",
            LocalizationKey = "Nav.Station",
            Icon = IconNames.IconTouch
        },
        new NavigationItemModel
        {
            ViewName = ViewNames.SetView,
            DisplayText = "设置",
            LocalizationKey = "Nav.Set",
            Icon = IconNames.IconSet
        },
        new NavigationItemModel
        {
            ViewName = ViewNames.SystemView,
            DisplayText = "系统",
            LocalizationKey = "Nav.System",
            Icon = IconNames.IconProcessing,
            PermissionCode = PermissionCodes.Engineer
        },
        new NavigationItemModel
        {
            ViewName = ViewNames.LogView,
            DisplayText = "日志",
            LocalizationKey = "Nav.Log",
            Icon = IconNames.IconLog
        }
    ];

    /// <summary>
    /// 二级导航集合，按所属一级视图分组。
    /// 工站仪表参数导航由 SetViewModel 根据当前 Machine.TestStations 动态前插。
    /// </summary>
    public Dictionary<string, List<NavigationItemModel>> SecondaryNavigationItems { get; set; } = new()
    {
        [ViewNames.SetView] =
        [
            new NavigationItemModel
            {
                ViewName = ViewNames.DiView,
                DisplayText = "DI 输入",
                LocalizationKey = "Nav.Di",
                PermissionCode = PermissionCodes.Admin
            },
            new NavigationItemModel
            {
                ViewName = ViewNames.DoView,
                DisplayText = "DO 输出",
                LocalizationKey = "Nav.Do",
                PermissionCode = PermissionCodes.Admin
            },
            new NavigationItemModel
            {
                ViewName = ViewNames.PlcPointView,
                DisplayText = "PLC 点位",
                LocalizationKey = "Nav.PlcPoint",
                PermissionCode = PermissionCodes.Admin
            },
        ],
        [ViewNames.SystemView] =
        [
            new NavigationItemModel
            {
                ViewName = ViewNames.ConnectView,
                DisplayText = "连接配置",
                LocalizationKey = "Nav.Connect"
            },
            new NavigationItemModel
            {
                DisplayText = "机种配置",
                Parameter = "MachineProfile.Basic"
            },
            new NavigationItemModel
            {
                DisplayText = "IO 点位设定",
                Parameter = "MachineProfile.IoPoints"
            },
            new NavigationItemModel
            {
                DisplayText = "PLC 点位设定",
                Parameter = "MachineProfile.PlcPoints"
            },
            new NavigationItemModel
            {
                DisplayText = "程序设定",
                LocalizationKey = "Nav.ProgramSettings",
                Parameter = "ProgramSettings"
            }
        ]
    };
}







