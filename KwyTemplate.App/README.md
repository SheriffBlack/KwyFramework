# KwyTemplate.App 导航设计

`KwyTemplate.App` 采用轻量导航设计，参考旧项目 `FundamentalUI` 的做法：

```text
NavigationConfigModel + ViewModel 直接导航
```

当前模板阶段页面数量不多，不再引入导航服务、导航状态持久化、动态贡献器等复杂机制。导航结构属于程序结构，优先放在代码中集中维护。

## 设计原则

- 导航数据集中放在 `Models/NavigationConfigModel.cs`。
- `MainViewModel` 读取一级导航集合。
- `SetViewModel` 读取二级导航集合。
- ViewModel 直接调用 `IRegionManager.RequestNavigate()`。
- 权限判断保留在导航点击入口。
- 不持久化导航选中状态；程序启动后进入默认页面。

## 主要类型

- `NavigationConfigModel`：集中维护一级、二级导航集合。
- `NavigationItemModel`：导航项模型，保留旧版常用字段，并增加 `PermissionCode`。
- `MainViewModel`：负责一级导航，默认进入 `HomeView`。
- `SetViewModel`：负责设置页二级导航，默认进入第一个子页面。

## NavigationItemModel

当前导航项模型保持简单：

```csharp
public class NavigationItemModel
{
    public string DisplayText { get; set; } = string.Empty;
    public string ViewName { get; set; } = string.Empty;
    public bool IsVisibility { get; set; } = true;
    public bool HasIcon => !string.IsNullOrWhiteSpace(Icon);
    public string Icon { get; set; } = string.Empty;
    public string Parameter { get; set; } = string.Empty;
    public string PermissionCode { get; set; } = string.Empty;
    public string NavigationKey => string.IsNullOrWhiteSpace(Parameter) ? ViewName : $"{ViewName}:{Parameter}";
}
```

其中：

- `ViewName`：目标 View 名称。
- `DisplayText`：导航按钮显示文本。
- `Icon`：导航图标资源名。
- `Parameter`：同一个 View 的不同实例参数。
- `PermissionCode`：进入该页面需要的权限码；为空表示不限制。
- `NavigationKey`：仅用于导航按钮选中状态绑定。

## 导航数据示例

```csharp
public sealed class NavigationConfigModel
{
    public List<NavigationItemModel> PrimaryNavigationItems { get; set; } =
    [
        new NavigationItemModel
        {
            ViewName = ViewNames.HomeView,
            DisplayText = "主页",
            Icon = IconNames.IconHome
        },
        new NavigationItemModel
        {
            ViewName = ViewNames.SystemView,
            DisplayText = "系统",
            Icon = IconNames.IconProcessing
        }
    ];

    public Dictionary<string, List<NavigationItemModel>> SecondaryNavigationItems { get; set; } = new()
    {
        [ViewNames.SetView] =
        [
            new NavigationItemModel
            {
                ViewName = ViewNames.DiView,
                DisplayText = "DI 输入"
            },
            new NavigationItemModel
            {
                ViewName = ViewNames.DoView,
                DisplayText = "DO 输出"
            }
        ]
    };
}
```

目标 Region 不放在导航模型中，而是由对应 ViewModel 决定：

- `MainViewModel` 导航到 `RegionNames.MainRegion`。
- `SetViewModel` 导航到 `RegionNames.SetRegion`。

这样导航模型更接近旧版 `FundamentalUI.Models.NavigationItemModel`，避免把 Region、排序、展开状态、持久化 Key 等复杂字段塞进模型。

## 新增页面

新增固定页面时，一般只需要：

1. 在 `NavigationConfigModel` 中添加导航项。
2. 在 `AppModule` 中注册对应 View / ViewModel。
3. 如果新增 Region，在对应 XAML 中声明 `RegionName`，并在对应 ViewModel 中调用 `RequestNavigate()`。

## 权限说明

如果某个导航项需要权限，可以设置：

```csharp
new NavigationItemModel
{
    ViewName = ViewNames.SetView,
    DisplayText = "设置",
    Icon = IconNames.IconSet,
    PermissionCode = PermissionCodes.Engineer
}
```

`MainViewModel` 在导航前检查 `PermissionCode`。权限不足时，通过 `IAppNotificationService` 统一提示并写入 LogView，同时保持当前选中页面不变。

## 为什么不持久化导航结构

导航项属于程序结构，不是现场运行参数。模板阶段把导航结构保存到 JSON 会带来额外问题：

- 配置文件误改会导致页面打不开。
- `ViewName`、权限码等运行契约被放到外部文件后更难排查。
- 当前业务只需要固定导航，持久化收益不明显。

如果后续确实需要“用户自定义菜单显示/隐藏/排序”，建议只持久化用户偏好，不要把完整导航契约外置。

## 专题文档

- [标准件、确认件与点检补偿界面设计](./StandardCompensateDesign.md)



