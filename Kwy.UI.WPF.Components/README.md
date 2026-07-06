# Kwy.UI.WPF.Components

`Kwy.UI.WPF.Components` 提供基于 `Kwy.UI.WPF` 和 `Kwy.MVVM.WPF` 的可直接使用组件。

业务项目通常只需要引用根命名空间：

```csharp
using Kwy.UI.WPF.Components;
```

注册组件：

```csharp
services.AddKwyWpfComponents();
```

XAML 命名空间：

```xml
xmlns:kwyc="http://schemas.kwy.com/ui/components"
```

## 消息弹窗

注入并使用消息对话框：

```csharp
public class ExampleViewModel
{
    private readonly IDialogMessageService dialogMessageService;

    public ExampleViewModel(IDialogMessageService dialogMessageService)
    {
        this.dialogMessageService = dialogMessageService;
    }

    public async Task DeleteAsync()
    {
        bool confirmed = await dialogMessageService.ShowConfirmAsync("确认删除吗？");
        if (!confirmed)
        {
            return;
        }
    }
}
```

需要完整按钮结果或更多显示选项时：

```csharp
ButtonResult result = await dialogMessageService.ShowAsync(
    "是否保存当前修改？",
    new DialogMessageOptions
    {
        Title = "保存修改",
        Icon = DialogMessageIcon.Question,
        ShowCancelButton = true
    });
```

## KwyPropertyGrid

`KwyPropertyGrid` 用于把普通配置对象渲染成可编辑属性表单。它通过 .NET 内置的 `System.ComponentModel` 特性，以及 Kwy 自定义的 `Kwy.ComponentModel` UI 元数据特性读取显示规则。

使用方式：

```xml
<kwyc:KwyPropertyGrid Source="{Binding DeviceParameter}" />
```

配置模型示例：

```csharp
public class InstrumentConfig
{
    [Category("基础设置")]
    [DisplayName("测试频率")]
    [InputType(InputType.TextBoxWithRadioButton)]
    [ItemsSource("Hz", "kHz", "MHz")]
    public double Frequency { get; set; } = 1000;

    [Browsable(false)]
    public string FrequencyUnit { get; set; } = "Hz";

    [Category("基础设置")]
    [DisplayName("触发模式")]
    [InputType(InputType.RadioButton)]
    [ItemsSource("EXT", "INT")]
    public string TriggerMode { get; set; } = "EXT";

    [Category("判定标准")]
    [DisplayName("启用比较")]
    public bool EnableCompare { get; set; }
}
```

支持的元数据：

- `CategoryAttribute`：属性分组。
- `DisplayNameAttribute`：显示名称。
- `Browsable(false)`：不显示该属性。
- `InputTypeAttribute`：指定编辑器类型。
- `ItemsSourceAttribute`：为 `ComboBox` / `RadioButton` / 单位选择提供候选项。
- `GroupWidthAttribute`：控制分组宽度比例。

默认推断规则：

- `bool`：`ToggleButton`
- `enum`：`ComboBox`
- `DateTime` / `DateTimeOffset`：`DatePicker`
- 可写普通属性：`TextBox`
- 只读属性：`TextBlock`

`DynamicPropertyItem` 和 `PropertyGroupModel` 位于 `Kwy.UI.WPF.Components.PropertyGrid`。它们是 WPF 属性表单的展示模型，不建议业务层直接依赖。

## KwyLoginView

`KwyLoginView` 是纯登录 UI 组件，只负责显示账号、密码、登录按钮和提示消息。它不依赖业务登录服务、不保存身份状态、不加载权限。

使用方式：

```xml
<kwyc:KwyLoginView
    UserName="{Binding UserName, Mode=TwoWay}"
    Password="{Binding Password, Mode=TwoWay}"
    UserNameItemsSource="{Binding UserNames}"
    LoginCommand="{Binding LoginCommand}"
    Message="{Binding LoginTip}" />
```

常用属性：

- `TitleText`：标题，默认 `用户登录`。
- `LoginButtonText`：按钮文字，默认 `登录`。
- `UserName`：账号，支持双向绑定。
- `Password`：密码，支持双向绑定。
- `UserNameItemsSource`：账号候选列表。
- `LoginCommand`：登录命令。
- `LoginCommandParameter`：登录命令参数。
- `Message`：错误或状态提示。
- `FormWidth` / `FormMinHeight` / `FormPadding`：表单尺寸，业务封装层可按项目需要调整。
- `InputWidth` / `InputHeight` / `ButtonHeight`：输入区尺寸，业务封装层可按项目需要调整。

业务 ViewModel 示例：

```csharp
public class LoginViewModel : BindableBase
{
    private readonly ILoginService loginService;
    private readonly IIdentityService identityService;
    private string userName = string.Empty;
    private string password = string.Empty;
    private string loginTip = string.Empty;

    public LoginViewModel(ILoginService loginService, IIdentityService identityService)
    {
        this.loginService = loginService;
        this.identityService = identityService;
        LoginCommand = new DelegateCommand(LoginAsync);
    }

    public IReadOnlyList<string> UserNames { get; } = ["操作员", "技术员", "工程师"];

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

        var user = await loginService.LoginAsync(UserName, Password);
        if (user == null)
        {
            LoginTip = "用户名/密码错误或账号禁用";
            return;
        }

        identityService.Login(user);
        LoginTip = "登录成功";
    }
}
```

作为对话框使用时，窗口外壳仍然复用组件库统一注册的 `KwyDialogWindow`。业务项目只需要注册自己的登录 View，并在 View 内容中放入 `KwyLoginView`；不需要为登录单独声明一套 Window 样式。

```xml
<UserControl
    x:Class="YourApp.Views.LoginView"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:kwyc="http://schemas.kwy.com/ui/components">

    <kwyc:KwyLoginView
        UserName="{Binding UserName, Mode=TwoWay}"
        Password="{Binding Password, Mode=TwoWay}"
        UserNameItemsSource="{Binding UserNames}"
        LoginCommand="{Binding LoginCommand}"
        Message="{Binding LoginTip}" />
</UserControl>
```

`AddKwyWpfComponents()` 会注册统一的 `IDialogWindow -> KwyDialogWindow`，因此消息弹窗、登录弹窗和后续组件弹窗都会共用同一个窗口承载样式。组件库不内置具体登录流程。
