# Kwy.MVVM

`Kwy.MVVM` 是 Kwy 系列的 UI 无关 MVVM 核心库。它只提供命令、通知、导航参数、全局消息总线、依赖注入入口和权限抽象，不引用 WPF、WinUI、MAUI 等具体 UI 平台。

平台相关能力放在对应平台项目中，例如：

- `Kwy.MVVM.WPF`：WPF 启动、Region、ViewModelLocator、Dialog、声明式权限等。
- `Kwy.UI.WPF` / `Kwy.UI.WPF.Components`：WPF 控件样式和通用组件。

## 核心模块

### 1. 基础 MVVM

- `BindableBase`：基于 `ObservableObject` 的属性通知基类。
- `DelegateCommand`：同步命令实现，支持 `CanExecute` 和状态刷新。
- `AsyncDelegateCommand`：异步命令实现，内置执行中状态和防重复执行。
- `KwyContainer`：全局服务定位入口，用于非构造注入场景下获取 DI 服务。

### 2. 全局消息总线

- `IMessageBus`：Kwy 推荐的新消息总线入口。
- `MessageBus`：基于 `CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger` 的薄封装。
- `MessagePublishOptions`：发布选项，例如显式缓存最后一条消息。
- `MessageSubscribeOptions<TMessage>`：订阅选项，例如 UI 线程、后台线程、过滤和热订阅回放。
- `IMessageDispatcher`：UI 调度抽象。WPF 实现位于 `Kwy.MVVM.WPF`。

Kwy 已移除 Prism 风格的 `IEventAggregator` / `PubSubEvent<T>`。原因是项目已经依赖 `CommunityToolkit.Mvvm`，`WeakReferenceMessenger` 原生支持弱引用消息、消息无需继承基类，也更适合直接使用 `record` / `class` 表达业务消息。

`IMessageBus` 是对 `WeakReferenceMessenger` 的 Kwy 风格适配层，额外统一了 UI 调度、热订阅回放、消息缓存和诊断钩子。

消息总线不要求消息继承基类，直接使用 `record` / `class` 即可：

```csharp
public sealed record DeviceStatusMessage(
    string DeviceId,
    bool IsOnline,
    int Temperature);
```

发布消息：

```csharp
messageBus.Publish(new DeviceStatusMessage("PLC-01", true, 25));
```

推荐订阅写法：

```csharp
subscription = messageBus.Subscribe(
    this,
    static (DeviceViewModel viewModel, DeviceStatusMessage message) =>
        viewModel.OnDeviceStatus(message));
```

这里的 `viewModel` 就是订阅时传入的 `this`，也就是订阅者自己。`static` lambda 不会捕获外部变量，因此不会偷偷强引用 ViewModel。

需要 UI 线程或热订阅时再传选项：

```csharp
subscription = messageBus.Subscribe(
    this,
    static (DeviceViewModel viewModel, DeviceStatusMessage message) =>
        viewModel.OnDeviceStatus(message),
    MessageSubscribeOptions<DeviceStatusMessage>.OnUIReplayLatest);
```

需要让新订阅者回放最后一条消息时，发布方必须显式保留最新消息：

```csharp
messageBus.Publish(
    new DeviceStatusMessage("PLC-01", true, 25),
    MessagePublishOptions.Retained);
```

消息总线适合：

- 用户登录切换
- 主题和语言切换
- 权限刷新
- 全局通知
- 设备状态摘要
- 模块间轻量消息

不建议用于：

- 高频相机图像帧
- 毫秒级运动轴位置
- 大量采样数据
- 海量日志流

这些高频数据应继续使用专用服务、`Channel<T>`、状态监控器或日志服务。

### 3. 参数与导航契约

- `IParameters`：统一参数集合，提供 `Add`、`GetValue`、`TryGetValue` 等方法。
- `NavigationParameters`：导航参数实现。
- `INavigationAware`：同步导航生命周期接口。
- `IAsyncNavigationAware`：异步导航生命周期接口。

### 4. 权限与授权

权限系统分为两层：

- `IPermissionService`：当前用户权限服务，回答“当前用户是否拥有某个权限”。
- `IAuthorizationService`：业务授权服务，回答“当前用户是否允许执行某个策略/操作”，可携带资源上下文。

核心库只定义抽象和命令装饰，不关心权限来自数据库、接口、配置文件还是登录令牌，也不直接控制 UI 的隐藏和禁用。WPF 控件层表现由 `Kwy.MVVM.WPF` 提供。

## 权限设计

### 为什么使用 IPermissionService

桌面应用的权限判断不应该让每个控件自己查数据库，也不应该把权限逻辑写进 Attached Property 或 ViewModel 的按钮状态里。推荐做法是：

1. 登录后由业务层加载当前用户权限。
2. `IPermissionService` 在内存中缓存当前用户权限。
3. 控件权限、命令权限、业务授权都查询同一个服务。
4. 权限变化时，服务触发 `PermissionsChanged`，由 UI 和命令自动刷新状态。

这样权限规则只有一份，UI 只是消费结果。

### IPermissionService

```csharp
public interface IPermissionService
{
    event EventHandler<PermissionChangedEventArgs>? PermissionsChanged;

    bool HasPermission(string permissionCode);

    string GetNoPermissionMessage(string permissionCode);
}
```

约定：

- `HasPermission` 应该是快速内存查询，不建议在这里直接访问数据库或远程接口。
- `GetNoPermissionMessage` 用于给命令、弹窗、日志或业务层返回统一提示。
- `PermissionsChanged` 用于通知权限刷新。
- `new PermissionChangedEventArgs("User.Edit")` 表示刷新单个权限。
- `new PermissionChangedEventArgs()` 表示全量刷新。

一个简单实现：

```csharp
public sealed class CurrentUserPermissionService : IPermissionService
{
    private readonly HashSet<string> permissions = new(StringComparer.Ordinal);

    public event EventHandler<PermissionChangedEventArgs>? PermissionsChanged;

    public bool HasPermission(string permissionCode)
        => permissions.Contains(permissionCode);

    public string GetNoPermissionMessage(string permissionCode)
        => $"当前用户没有权限：{permissionCode}";

    public void SetPermissions(IEnumerable<string> values)
    {
        permissions.Clear();
        foreach (var value in values)
        {
            permissions.Add(value);
        }

        PermissionsChanged?.Invoke(this, new PermissionChangedEventArgs());
    }
}
```

### IAuthorizationService

`IAuthorizationService` 面向业务操作。它比 `IPermissionService` 更靠近“是否允许做这件事”的语义，并预留了 `resource` 参数。

```csharp
public interface IAuthorizationService
{
    ValueTask<AuthorizationResult> AuthorizeAsync(
        string policy,
        object? resource = null,
        CancellationToken cancellationToken = default);
}
```

默认实现 `PermissionAuthorizationService` 会把 `policy` 当作权限码，内部调用 `IPermissionService.HasPermission(policy)`。

适合默认实现的场景：

```csharp
var result = await authorizationService.AuthorizeAsync("User.Delete");
if (!result.Succeeded)
{
    return;
}
```

如果后续需要资源级授权，可以替换自己的实现：

```csharp
public sealed class AppAuthorizationService : IAuthorizationService
{
    private readonly IPermissionService permissionService;

    public AppAuthorizationService(IPermissionService permissionService)
    {
        this.permissionService = permissionService;
    }

    public ValueTask<AuthorizationResult> AuthorizeAsync(
        string policy,
        object? resource = null,
        CancellationToken cancellationToken = default)
    {
        if (policy == "Order.Edit" && resource is Order order && order.IsClosed)
        {
            return ValueTask.FromResult(AuthorizationResult.Failure("已关闭订单不可编辑。"));
        }

        return ValueTask.FromResult(
            permissionService.HasPermission(policy)
                ? AuthorizationResult.Success()
                : AuthorizationResult.Failure(permissionService.GetNoPermissionMessage(policy)));
    }
}
```

## 命令权限

`PermissionCommandDecorator` 可以给任意 `ICommand` 增加权限检查。

```csharp
public ICommand DeleteCommand { get; }

public UserViewModel(IPermissionService permissionService)
{
    DeleteCommand = new DelegateCommand(Delete)
        .WithPermission(permissionService, "User.Delete", PermissionCheckMode.Disable);
}
```

也可以在已经配置好 `KwyContainer.Current` 的应用中省略服务参数：

```csharp
DeleteCommand = new DelegateCommand(Delete)
    .WithPermission("User.Delete", PermissionCheckMode.Disable);
```

推荐优先显式传入 `IPermissionService`，这样 ViewModel 更容易测试，也不会隐式依赖全局容器。

### PermissionCheckMode

- `Disable`：无权限时 `CanExecute` 返回 `false`。
- `Prompt`：保留给提示型权限模式；核心库不弹窗。
- `Hide`：主要给 UI attached property 使用，命令层不会隐藏控件。
- `Both`：命令层按 `Disable` 处理，UI 层同时禁用和隐藏。

命令装饰器会订阅 `IPermissionService.PermissionsChanged`。当权限变化时，它会触发 `CanExecuteChanged`，按钮状态会随权限刷新。

## DI 注册

基础注册方式：

```csharp
services.AddSingleton<IPermissionService, CurrentUserPermissionService>();
services.AddSingleton<IAuthorizationService, PermissionAuthorizationService>();
```

在 `Kwy.MVVM.WPF.KwyApplication` 中，框架会尝试从容器中解析 `IPermissionService`，并设置给 WPF 权限 attached property 的默认服务。业务项目只需要注册自己的 `IPermissionService`。

如果需要替换授权策略：

```csharp
services.AddSingleton<IPermissionService, CurrentUserPermissionService>();
services.AddSingleton<IAuthorizationService, AppAuthorizationService>();
```

## WPF 声明式权限

WPF 控件权限不在 `Kwy.MVVM` 中实现，而是在 `Kwy.MVVM.WPF` 的 `Permission` attached property 中实现。

```xml
<Button Content="删除"
        kwy:Permission.Policy="User.Delete"
        kwy:Permission.Mode="Hide" />
```

`Policy` 和 `Code` 都可以使用：

- `Policy`：推荐新写法，语义更接近授权策略。
- `Code`：保留给传统权限码场景。

如果同时设置，`Policy` 优先。

也可以给某个控件单独指定权限服务：

```csharp
Permission.SetService(deleteButton, permissionService);
Permission.SetPolicy(deleteButton, "User.Delete");
```

## 推荐实践

- 权限码建议使用稳定字符串，例如 `User.Delete`、`Recipe.Edit`、`Device.Motion.Start`。
- `IPermissionService` 缓存当前用户权限，不在控件或命令中访问数据库。
- UI 权限和命令权限共享同一个 `IPermissionService`。
- 关键业务操作仍然调用 `IAuthorizationService` 做最终校验，不只依赖按钮禁用或隐藏。
- 权限变化后触发 `PermissionsChanged`，让 UI 和命令自动刷新。
- ViewModel 中优先显式注入 `IPermissionService` 或 `IAuthorizationService`，减少对全局容器的依赖。

## 旧 API 迁移

旧权限兼容入口已经移除：

- `IPermissionProvider`
- `PermissionChangedMessage`
- `PermissionProviderAdapter`
- `PermissionDeniedHandler`

迁移方式：

```csharp
// 旧：实现 IPermissionProvider
// 新：实现 IPermissionService

public sealed class CurrentUserPermissionService : IPermissionService
{
    public event EventHandler<PermissionChangedEventArgs>? PermissionsChanged;

    public bool HasPermission(string permissionCode)
    {
        // 查询当前用户权限缓存
        return true;
    }

    public string GetNoPermissionMessage(string permissionCode)
        => $"当前用户没有权限：{permissionCode}";
}
```

命令迁移：

```csharp
DeleteCommand = new DelegateCommand(Delete)
    .WithPermission(permissionService, "User.Delete");
```

业务授权迁移：

```csharp
var result = await authorizationService.AuthorizeAsync("User.Delete", selectedUser);
if (!result.Succeeded)
{
    return;
}
```

## 依赖

- `.NET 8.0+`
- `Microsoft.Extensions.DependencyInjection`
- `CommunityToolkit.Mvvm`
