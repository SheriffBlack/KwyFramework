# Kwy.UI.WPF.Components

`Kwy.UI.WPF.Components` 是建立在 `Kwy.UI.WPF`、`Kwy.MVVM.WPF` 和
`Kwy.ComponentModel` 之上的 WPF 组合组件库。

它不负责提供最底层的按钮、文本框或窗口控件，而是把这些基础能力组合成业务项目可以直接使用的界面功能：

- 统一样式的消息对话框；
- 文本、数值输入对话框；
- Toast 消息通知；
- UI 活动日志列表；
- 元数据驱动的 `KwyPropertyGrid`；
- 可由业务登录逻辑驱动的 `KwyLoginView`；
- 所有组件弹窗共用的 `KwyDialogWindow`。

## 1. 项目定位

组件库的依赖方向如下：

```text
业务项目
  └─ Kwy.UI.WPF.Components       可直接使用的组合组件
       ├─ Kwy.UI.WPF             基础 WPF 控件、主题和样式
       ├─ Kwy.MVVM.WPF           对话框、导航和 WPF MVVM 支持
       ├─ Kwy.MVVM               命令、BindableBase 等基础能力
       └─ Kwy.ComponentModel     与 UI 平台无关的属性元数据
```

业务规则、设备访问、用户认证和日志持久化不应放入本项目。组件库只负责通用 UI 展示、交互和绑定适配。

## 2. 快速开始

### 2.1 注册组件服务

在应用的依赖注入配置中调用：

```csharp
using Kwy.UI.WPF.Components;

services.AddKwyWpfComponents();
```

`AddKwyWpfComponents()` 当前注册：

| 服务 | 生命周期 | 用途 |
| --- | --- | --- |
| `KwyActivityLogService` | Singleton | 操作员界面的活动摘要缓冲区 |
| `IDialogMessageService` | Singleton | 标准消息对话框 |
| `IInputDialogService` | Singleton | 文本、数值输入对话框 |
| `IToastMessageService` | Singleton | Toast 消息通知 |
| `IDialogWindow -> KwyDialogWindow` | Transient | 统一对话框窗口外壳 |
| `DialogMessageView` / `DialogMessageViewModel` | Transient | 以 View 名称进行键控注册，用作消息弹窗内容 |
| `InputDialogView` / `InputDialogViewModel` | Transient | 以 View 名称进行键控注册，用作输入弹窗内容 |

消息弹窗、输入弹窗以及业务项目后续注册的组件弹窗，都可以复用统一的 `KwyDialogWindow` 外壳和主题样式。

`RegisterForNavigation<TView, TViewModel>()` 不是一种生命周期。它负责注册 View、ViewModel 及两者的映射关系；默认把 View 和 ViewModel 注册为 `Transient`，只有显式传入 `isSingleton: true` 时才注册为 `Singleton`。

### 2.2 XAML 命名空间

组件库已把公开控件映射到统一的 XAML 命名空间：

```xml
xmlns:kwyc="http://schemas.kwy.com/ui/components"
```

基础控件仍使用 `Kwy.UI.WPF` 的命名空间：

```xml
xmlns:kwyui="http://schemas.kwy.com/ui"
```

典型页面声明：

```xml
<UserControl
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:kwyc="http://schemas.kwy.com/ui/components"
    xmlns:kwyui="http://schemas.kwy.com/ui">
</UserControl>
```

## 3. 消息对话框

消息对话框适用于确认、警告、错误和普通提示等需要阻塞当前操作、等待用户选择的交互。

### 3.1 注入服务

```csharp
using Kwy.UI.WPF.Components.Dialogs.Message;

public sealed class ExampleViewModel
{
    private readonly IDialogMessageService dialogMessageService;

    public ExampleViewModel(
        IDialogMessageService dialogMessageService)
    {
        this.dialogMessageService = dialogMessageService;
    }
}
```

### 3.2 常用方法

```csharp
await dialogMessageService.ShowInfoAsync(
    "连接成功",
    "提示");

await dialogMessageService.ShowWarningAsync(
    "PLC 未连接",
    "警告");

await dialogMessageService.ShowErrorAsync(
    "写入失败",
    "错误");

bool confirmed = await dialogMessageService.ShowConfirmAsync(
    "确认执行该操作吗？",
    "确认操作");
```

确认删除示例：

```csharp
public async Task DeleteAsync()
{
    bool confirmed = await dialogMessageService
        .ShowConfirmAsync("确认删除吗？");

    if (!confirmed)
    {
        return;
    }

    // 执行删除。
}
```

### 3.3 完整按钮结果

需要区分具体按钮结果或自定义按钮文字时，使用 `ShowAsync`：

```csharp
using Kwy.MVVM.Dialogs;

ButtonResult result = await dialogMessageService.ShowAsync(
    "是否保存当前修改？",
    new DialogMessageOptions
    {
        Title = "保存修改",
        Icon = DialogMessageIcon.Question,
        ShowCancelButton = true,
        ConfirmButtonText = "保存",
        CancelButtonText = "放弃"
    });
```

`DialogMessageOptions`：

| 属性 | 说明 |
| --- | --- |
| `Title` | 对话框标题 |
| `Icon` | `None`、`Success`、`Info`、`Warning`、`Error` 或 `Question` |
| `ShowCancelButton` | 是否显示取消按钮 |
| `ConfirmButtonText` | 确认按钮文字 |
| `CancelButtonText` | 取消按钮文字 |

## 4. 输入对话框

输入对话框用于收集一个简单值，例如 PLC 写入值、目标数量、延时时间或配方名称。

### 4.1 注入服务

```csharp
using Kwy.UI.WPF.Components.Dialogs.Input;

public sealed class ExampleViewModel
{
    private readonly IInputDialogService inputDialogService;

    public ExampleViewModel(
        IInputDialogService inputDialogService)
    {
        this.inputDialogService = inputDialogService;
    }
}
```

### 4.2 数值输入

```csharp
InputDialogResult result = await inputDialogService.ShowNumberAsync(
    message: "请输入 PLC 写入值",
    title: "PLC 写入",
    defaultValue: 0,
    minimum: 0,
    maximum: 1000,
    unit: "int32");

if (!result.IsConfirmed)
{
    return;
}

int value = result.GetInt32();
```

数值结果支持：

```csharp
if (result.TryGetDecimal(out decimal value))
{
    // 使用 value。
}

decimal requiredValue = result.GetDecimal();
int requiredInteger = result.GetInt32();
```

`TryGetDecimal` 会依次使用当前区域性和固定区域性解析输入。

### 4.3 文本输入

```csharp
InputDialogResult result = await inputDialogService.ShowTextAsync(
    message: "请输入配方名称",
    title: "新建配方",
    defaultValue: "Recipe001");

if (result.IsConfirmed)
{
    string recipeName = result.Value;
}
```

### 4.4 完整配置

```csharp
InputDialogResult result = await inputDialogService.ShowAsync(
    new InputDialogOptions
    {
        Title = "设置目标数量",
        ShowContentTitle = true,
        Message = "请输入本批次目标数量。",
        Label = "目标数量",
        DefaultValue = "100",
        InputType = InputDialogType.Number,
        Minimum = 1,
        Maximum = 999999,
        Unit = "pcs",
        ConfirmButtonText = "保存",
        CancelButtonText = "取消",
        ShowCancelButton = true
    });
```

`InputDialogOptions`：

| 属性 | 说明 |
| --- | --- |
| `Title` | 窗口标题 |
| `ShowContentTitle` | 是否在弹窗内容区域再次显示标题 |
| `Message` | 输入说明文字 |
| `Label` | 输入项标签 |
| `DefaultValue` | 默认输入值 |
| `InputType` | `Text` 或 `Number` |
| `Minimum` / `Maximum` | 数值输入范围 |
| `Unit` | 输入框右侧单位，例如 `ms`、`pcs`、`Ω` |
| `ConfirmButtonText` / `CancelButtonText` | 按钮文字 |
| `ShowCancelButton` | 是否显示取消按钮 |

`InputDialogView` 只负责显示，`InputDialogViewModel` 负责状态、命令和基础校验。业务项目应调用 `IInputDialogService`，不要直接创建窗口或内部 ViewModel。

## 5. Toast 消息

Toast 适用于不需要阻塞用户操作的短消息，例如“保存成功”“设备已连接”或“参数下发失败”。

### 5.1 放置 Toast Host

页面必须存在一个 `KwyToastHost`，并通过附加属性注册 Token：

```xml
<Grid>
    <!-- 页面主要内容。 -->

    <kwyui:KwyToastHost
        kwyc:ToastHostRegistration.Token="RootToast" />
</Grid>
```

默认 Token 是：

```csharp
ToastTokens.Root // "RootToast"
```

Host 加载时自动注册，卸载时自动解除注册。服务使用弱引用保存 Host，不会因为 Toast 服务是单例而强制延长页面生命周期。

### 5.2 显示消息

```csharp
using Kwy.UI.WPF.Components;
using Kwy.UI.WPF.Components.Toasts;

public sealed class ExampleViewModel
{
    private readonly IToastMessageService toast;

    public ExampleViewModel(IToastMessageService toast)
    {
        this.toast = toast;
    }

    public void Notify()
    {
        toast.ShowSuccess("参数保存成功");
        toast.ShowInfo("设备已连接");
        toast.ShowWarning("当前数据尚未保存");
        toast.ShowError("PLC 写入失败");
    }
}
```

指定时长和 Host：

```csharp
toast.ShowWarning(
    "请检查安全门状态",
    duration: TimeSpan.FromSeconds(5),
    token: "MachineToast");
```

完整配置：

```csharp
toast.Show(
    "任务已提交",
    new ToastMessageOptions
    {
        Token = ToastTokens.Root,
        Icon = DialogMessageIcon.Success,
        Duration = TimeSpan.FromSeconds(3)
    });
```

清空消息：

```csharp
toast.Clear();
toast.Clear("MachineToast");
```

如果没有找到对应 Token 的 Host，`Show` 会安全返回，不会创建额外窗口。

## 6. UI 活动日志

`KwyActivityLogService` 是面向操作员界面的轻量内存缓冲区，`KwyActivityLogListView` 负责显示活动摘要。

它适合显示用户和现场人员需要看到的运行摘要，不负责替代 `Microsoft.Extensions.Logging`、Serilog 或文件日志。
如果业务需要在程序重启后保留操作员历史，应由应用层把同一份脱敏活动记录
写入独立的持久化 Sink；组件库本身不负责文件存储。

### 6.1 添加日志

```csharp
using Kwy.UI.WPF.Components.ActivityLog;

public sealed class ExampleViewModel
{
    public ExampleViewModel(KwyActivityLogService activityLog)
    {
        ActivityLog = activityLog;
    }

    public KwyActivityLogService ActivityLog { get; }

    public void ReportResult()
    {
        ActivityLog.Info("工位已启动");
        ActivityLog.Success("测试通过");
        ActivityLog.Warn("测量值接近上限");
        ActivityLog.Error("仪表通信失败");
    }
}
```

### 6.2 显示日志

```xml
<kwyc:KwyActivityLogListView
    ItemsSource="{Binding ActivityLog.Entries}"
    AutoScroll="True"
    ShowTime="True"
    ShowLevel="True"
    MessageTextWrapping="NoWrap" />
```

公开显示属性：

| 属性 | 默认值 | 说明 |
| --- | --- | --- |
| `ItemsSource` | `null` | 日志数据源 |
| `AutoScroll` | `true` | 新日志到达后是否自动滚动 |
| `ShowTime` | `true` | 是否显示时间列 |
| `ShowLevel` | `true` | 是否显示级别列 |
| `MessageTextWrapping` | `NoWrap` | 消息是否换行 |

日志项至少需要提供：

```csharp
public interface IKwyActivityLogEntry
{
    string TimeText { get; }
    string Level { get; }
    string Message { get; }
}
```

业务可以直接使用 `KwyActivityLogEntry`，也可以提供具有相同绑定属性的自定义对象。

### 6.3 缓冲区规则

- 默认最多保留最近 `2000` 条；
- 可通过 `KwyActivityLogService.MaxCount` 调整；
- 超过上限时删除最早加入的日志；
- 从后台线程写入时，会自动调度到 WPF Dispatcher；
- 应用关闭阶段无法进入 UI 队列的日志会被安全丢弃；
- `AddStartupProgress` 支持根据 `SortOrder` 对启动阶段日志排序。

推荐分层：

```text
开发诊断日志 → ILogger / 文件日志
用户可见摘要 → KwyActivityLogService → KwyActivityLogListView
```

设备层和流程层不应直接依赖 `KwyActivityLogListView`。

## 7. KwyPropertyGrid

`KwyPropertyGrid` 把普通 CLR 配置对象渲染成可编辑表单。

模型通过 `System.ComponentModel` 和 `Kwy.ComponentModel` 特性描述 UI 元数据；组件通过反射读取元数据，再由 WPF `DataTemplate` 选择具体编辑控件。

### 7.1 基本使用

```xml
<kwyc:KwyPropertyGrid
    Padding="0"
    IsEnabled="{Binding CanEdit}"
    Source="{Binding DeviceParameter}" />
```

`Source` 可以是任意普通对象。Source 改变时，PropertyGrid 会重新读取对象元数据并构造表单。

### 7.2 完整模型示例

```csharp
using System.ComponentModel;
using Kwy.ComponentModel;
using Kwy.ComponentModel.Attributes;

public sealed class InstrumentConfig
{
    [Category("基础设置")]
    [DisplayName("启用测试")]
    [InputType(InputType.ToggleButton)]
    [RefreshPropertyGrid]
    public bool IsEnabled { get; set; } = true;

    [Category("基础设置")]
    [DisplayName("测试频率")]
    [InputType(InputType.TextBoxWithRadioButton)]
    [ItemsSource("Hz", "kHz", "MHz")]
    [DisableWhen(nameof(IsEnabled), false)]
    public double Frequency { get; set; } = 1000;

    [Browsable(false)]
    public string FrequencyUnit { get; set; } = "Hz";

    [Category("基础设置")]
    [DisplayName("触发模式")]
    [InputType(InputType.RadioButton)]
    [ItemsSource("EXT", "INT")]
    public string TriggerMode { get; set; } = "EXT";

    [Category("判定标准")]
    [DisplayName("下限")]
    [InputType(InputType.NumberBox)]
    [NumberRange(0, 1000, SmallChange = 0.1, DecimalPlaces = 3)]
    [EditorWidth(140)]
    [VisibleWhen(nameof(IsEnabled))]
    public double LowerLimit { get; set; }

    [Category("通信")]
    [DisplayName("串口")]
    [InputType(InputType.ComboBox)]
    [ItemsSourceProvider(nameof(GetAvailablePorts))]
    public string PortName { get; set; } = string.Empty;

    public IEnumerable<string> GetAvailablePorts()
        => System.IO.Ports.SerialPort.GetPortNames();
}
```

### 7.3 当前支持的编辑器

| `InputType` | WPF 编辑器 | 绑定行为 |
| --- | --- | --- |
| `TextBox` | `TextBox` | 失去焦点时更新模型 |
| `NumberBox` | `KwyNumberBox` | 值改变时更新模型 |
| `ComboBox` | `ComboBox` | 选中项改变时更新模型 |
| `DatePicker` | `DatePicker` | 日期改变时更新模型 |
| `ToggleButton` | 开关样式 `ToggleButton` | 状态改变时更新模型 |
| `RadioButton` | `KwyRadioButtonGroup` | 选择改变时更新模型 |
| `TextBoxWithRadioButton` | `TextBox` + 单选单位组 | 编辑值和对应单位属性 |
| `TextBlock` | `TextBlock` | 只读显示 |

`InputType` 协议中还定义了 `Button` 和 `ListBox`，但当前 `KwyPropertyGrid` 尚未提供对应专用模板；不要在 PropertyGrid 模型中依赖这两个类型。

### 7.4 默认编辑器推断

没有显式声明 `[InputType]` 时：

| CLR 类型 | 默认编辑器 |
| --- | --- |
| `bool` | `ToggleButton` |
| `enum` | `ComboBox`，候选项自动取枚举值 |
| `DateTime` / `DateTimeOffset` | `DatePicker` |
| 数值类型 | `NumberBox` |
| 只读属性 | `TextBlock` |
| 其他可写属性 | `TextBox` |

显式 `[InputType]` 的优先级高于默认推断。

### 7.5 支持的元数据特性

| 特性 | 作用 |
| --- | --- |
| `[Category]` | 设置属性分组 |
| `[CategoryKey]` | 设置分组名称的多语言资源键 |
| `[CategorySource]` | 从当前对象的另一个属性动态取得分组名称 |
| `[DisplayName]` | 设置属性显示名称和多语言后备文字 |
| `[DisplayNameKey]` | 设置属性名称的多语言资源键 |
| `[Browsable(false)]` | 隐藏属性 |
| `[ReadOnly(true)]` | 只读显示 |
| `[InputType]` | 显式选择编辑器 |
| `[ItemsSource]` | 声明静态候选项 |
| `[ItemsSourceProvider]` | 从公共属性或无参公共方法取得动态候选项 |
| `[NumberRange]` | 数值最小值、最大值、步长和小数位数 |
| `[EditorWidth]` | 编辑器宽度 |
| `[GroupWidth]` | 分组占可用宽度的比例，例如 `0.5` |
| `[InlineGroup]` | 把属性标记为独立的行内分组块 |
| `[VisibleWhen]` | 根据同一对象中的布尔属性决定是否显示 |
| `[DisableWhen]` | 根据同一对象中的布尔属性决定是否禁用 |
| `[RefreshPropertyGrid]` | 属性改变后重新生成表单 |

用于 `[VisibleWhen]`、`[DisableWhen]`、`[CategorySource]` 和 `[ItemsSourceProvider]` 的成员名称必须指向同一 Source 对象上的公共成员。

### 7.6 数值范围

```csharp
[InputType(InputType.NumberBox)]
[NumberRange(
    minimum: 0,
    maximum: 100,
    SmallChange = 0.5,
    DecimalPlaces = 2)]
public double Threshold { get; set; }
```

未显式指定时：

- 整数步长默认 `1`、小数位数默认 `0`；
- 浮点数步长默认 `0.1`、小数位数默认 `3`。

### 7.7 数值与单位组合

`TextBoxWithRadioButton` 使用约定命名查找单位属性：

```text
值属性：Frequency
单位属性：FrequencyUnit
```

示例：

```csharp
[InputType(InputType.TextBoxWithRadioButton)]
[ItemsSource("Hz", "kHz", "MHz")]
public double Frequency { get; set; }

[Browsable(false)]
public string FrequencyUnit { get; set; } = "Hz";
```

单位属性通常使用 `[Browsable(false)]`，避免它作为独立属性再次显示。

### 7.8 动态候选项

Provider 可以是公共可读属性：

```csharp
[ItemsSourceProvider(nameof(AvailableModes))]
public string Mode { get; set; } = string.Empty;

[Browsable(false)]
public IReadOnlyList<string> AvailableModes { get; }
    = ["Auto", "Manual"];
```

也可以是无参、非 `void` 的公共方法：

```csharp
[ItemsSourceProvider(nameof(GetAvailablePorts))]
public string PortName { get; set; } = string.Empty;

public IEnumerable<string> GetAvailablePorts()
    => SerialPort.GetPortNames();
```

组件只缓存 Provider 的调用方式，不缓存返回结果，所以 Provider 每次被读取时仍可返回最新数据。

### 7.9 条件显示与刷新

如果某个属性影响其他属性的可见性或编辑状态，应同时添加 `[RefreshPropertyGrid]`：

```csharp
[RefreshPropertyGrid]
public bool UseAdvancedOptions { get; set; }

[VisibleWhen(nameof(UseAdvancedOptions))]
public double AdvancedThreshold { get; set; }
```

控制属性改变后，PropertyGrid 会重新构建分组和属性项，从而应用新的显示规则。

### 7.10 多语言

模型可同时声明后备文字和资源键：

```csharp
[Category("A班")]
[CategoryKey("CompensateOptions.Category.ShiftA")]
[DisplayName("时间1（时）")]
[DisplayNameKey("CompensateOptions.Time1")]
public string Time1 { get; set; } = "8";
```

查找顺序：

```text
WPF 动态资源键
  ↓ 找不到或值无效
Category / DisplayName 后备文字
```

应用切换语言、替换资源字典后调用：

```csharp
PropertyMetadataLocalization.NotifyChanged();
```

现有 PropertyGrid 会刷新分组名称和属性显示名称，不必重建业务配置对象。

### 7.11 反射与缓存

PropertyGrid 的运行链路：

```text
Source 对象
  ↓
PropertyMetadataReader
  ├─ 反射公共属性和 Attribute
  └─ 按 Type 缓存 PropertyMetadataItem
  ↓
PropertyGridMetadataReader
  ├─ 可见性过滤
  └─ 分类、分组
  ↓
DynamicPropertyItem
  ├─ Getter / Setter 表达式访问器缓存
  └─ ItemsSourceProvider 调用器缓存
  ↓
KwyPropertyGrid.xaml
  └─ 根据 InputType 选择 DataTemplate
```

缓存边界：

- 元数据按照 `Type` 缓存；
- Getter/Setter 按照 `PropertyInfo` 缓存；
- 动态 Provider 调用器按照 `(SourceType, ProviderName)` 缓存；
- 不缓存 Source 实例、当前属性值或动态 Provider 返回结果。

因此重新进入页面时仍会创建新的展示模型并读取当前 Source 数据，但不会为同一属性反复编译表达式访问器。

`DynamicPropertyItem`、`PropertyGroupModel` 和元数据读取器属于 PropertyGrid 内部展示与适配机制，业务层通常不应直接依赖。

## 8. KwyLoginView

`KwyLoginView` 是纯登录表单组件，只负责显示账号、密码、按钮和状态消息。

它不会：

- 访问用户数据库；
- 验证账号密码；
- 保存当前身份；
- 加载权限；
- 决定登录成功后导航到哪里。

这些职责由业务项目的 ViewModel 和认证服务完成。

### 8.1 使用方式

```xml
<kwyc:KwyLoginView
    TitleText="用户登录"
    LoginButtonText="登录"
    UserName="{Binding UserName, Mode=TwoWay}"
    Password="{Binding Password, Mode=TwoWay}"
    UserNameItemsSource="{Binding UserNames}"
    LoginCommand="{Binding LoginCommand}"
    LoginCommandParameter="{Binding}"
    Message="{Binding LoginTip}" />
```

### 8.2 公开属性

| 属性 | 默认值 | 说明 |
| --- | --- | --- |
| `TitleText` | `用户登录` | 表单标题 |
| `LoginButtonText` | `登录` | 登录按钮文字 |
| `UserName` | 空字符串 | 账号，默认双向绑定 |
| `Password` | 空字符串 | 密码，默认双向绑定 |
| `UserNameItemsSource` | `null` | 账号候选列表 |
| `LoginCommand` | `null` | 登录命令 |
| `LoginCommandParameter` | `null` | 登录命令参数 |
| `Message` | 空字符串 | 错误或状态提示 |
| `FormWidth` | `500` | 表单宽度 |
| `FormMinHeight` | `500` | 表单最小高度 |
| `FormPadding` | `70,48,70,48` | 表单内边距 |
| `InputWidth` | `300` | 输入控件宽度 |
| `InputHeight` | `45` | 输入控件高度 |
| `ButtonHeight` | `50` | 按钮高度 |

### 8.3 业务 ViewModel 示例

```csharp
public sealed class LoginViewModel : BindableBase
{
    private readonly ILoginService loginService;
    private string userName = string.Empty;
    private string password = string.Empty;
    private string loginTip = string.Empty;

    public LoginViewModel(ILoginService loginService)
    {
        this.loginService = loginService;
        LoginCommand = new DelegateCommand(LoginAsync);
    }

    public IReadOnlyList<string> UserNames { get; }
        = ["操作员", "工程师", "管理员"];

    public string UserName
    {
        get => userName;
        set => SetProperty(ref userName, value);
    }

    public string Password
    {
        get => password;
        set => SetProperty(ref password, value);
    }

    public string LoginTip
    {
        get => loginTip;
        set => SetProperty(ref loginTip, value);
    }

    public DelegateCommand LoginCommand { get; }

    private async void LoginAsync()
    {
        LoginTip = string.Empty;

        if (string.IsNullOrWhiteSpace(UserName))
        {
            LoginTip = "请输入用户名";
            return;
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            LoginTip = "请输入密码";
            return;
        }

        var user = await loginService.LoginAsync(
            UserName,
            Password);

        LoginTip = user == null
            ? "用户名、密码错误或账号禁用"
            : "登录成功";
    }
}
```

实际项目中建议使用支持异步的命令类型，避免在 `async void` 命令方法中遗漏异常处理。这里为了突出组件绑定关系而保持示例简洁。

## 9. 统一对话框窗口

`KwyDialogWindow` 继承自 `KwyWindow` 并实现 `IDialogWindow`，作为组件弹窗的统一窗口外壳。

```text
IDialogMessageService / IInputDialogService / 业务弹窗服务
  ↓
Kwy.MVVM.WPF 对话框机制
  ↓
IDialogWindow
  ↓
KwyDialogWindow
```

业务弹窗通常只需要定义 `UserControl` 和 ViewModel，不需要再创建一套 Window 样式。窗口遮罩、Owner 关系、主题和公共外观由统一窗口承载。

## 10. 选择哪一种反馈组件

| 场景 | 推荐组件 |
| --- | --- |
| 必须让用户确认后才能继续 | `IDialogMessageService` |
| 需要用户输入一个文本或数值 | `IInputDialogService` |
| 只需短暂提示，不阻塞操作 | `IToastMessageService` |
| 持续显示运行摘要 | `KwyActivityLogService` + `KwyActivityLogListView` |
| 编辑一个配置对象 | `KwyPropertyGrid` |
| 复用登录表单外观 | `KwyLoginView` |

不要用 Toast 代替需要确认的安全操作，也不要把 `KwyActivityLogService` 当作完整历史日志存储。

## 11. 推荐的业务边界

```text
View
  ├─ 放置 KwyPropertyGrid、KwyActivityLogListView、KwyLoginView、KwyToastHost
  └─ 绑定 ViewModel

ViewModel / UI Service
  ├─ 调用消息、输入和 Toast 服务
  ├─ 暴露配置对象或日志集合
  └─ 处理用户命令

Application / Equipment / Process
  ├─ 执行业务规则
  ├─ 访问设备和流程状态
  └─ 发布业务事件，不依赖具体 WPF 控件
```

组件库的目标是统一交互和外观，而不是把业务逻辑搬进 UI 控件。
