# Kwy.MVVM.WPF

`Kwy.MVVM.WPF` 是 Kwy MVVM 在 WPF 平台上的实现层。它负责把 `Kwy.MVVM` 的核心抽象接入 WPF 的依赖属性、视觉树、窗口、Region 和应用启动流程。

核心原则：

- `Kwy.MVVM` 保持 UI 无关。
- `Kwy.MVVM.WPF` 只处理 WPF 平台行为。
- 业务权限、导航、对话框等能力通过接口注入，不直接绑定具体业务实现。

## 核心能力

### ViewModelLocator

通过命名约定或显式注册为 View 自动创建并绑定 ViewModel。

```xml
<UserControl kwy:ViewModelLocator.AutoWireViewModel="True" />
```

### RegionManager

提供 WPF 区域导航能力。

```xml
<ContentControl kwy:RegionManager.RegionName="MainContent" />
```

```csharp
await regionManager.RequestNavigateAsync("MainContent", "HomeView");
```

### ViewCacheManager

用于缓存导航离开的视图实例，减少高频页面切换中的 XAML 创建成本。

```csharp
var cache = serviceProvider.GetRequiredService<IViewCacheManager>();
cache.DefaultCacheExpiration = TimeSpan.FromMinutes(5);
cache.CleanupInterval = TimeSpan.FromSeconds(30);
```

### DialogService

提供基于 `IDialogWindow` / `IDialogAware` 的对话框服务。业务侧通过服务打开弹窗，不需要直接调用 `Window.ShowDialog()`。

```csharp
services.AddTransient<IDialogWindow, MyDialogWindow>();
```

## 声明式权限

WPF 权限表现由 `Kwy.MVVM.WPF.Permissions.Permission` attached property 提供，底层权限判断统一使用 `Kwy.MVVM.Core.IPermissionService`。

### 注册权限服务

```csharp
services.AddSingleton<IPermissionService, CurrentUserPermissionService>();
services.AddSingleton<IAuthorizationService, PermissionAuthorizationService>();
```

`KwyApplication` 启动后会从容器中解析 `IPermissionService`，并设置为 WPF 权限系统的默认服务。

### XAML 使用

推荐使用 `Policy`：

```xml
<Button Content="删除用户"
        kwy:Permission.Policy="User.Delete"
        kwy:Permission.Mode="Hide" />
```

也可以使用传统权限码写法：

```xml
<Button Content="编辑用户"
        kwy:Permission.Code="User.Edit"
        kwy:Permission.Mode="Disable" />
```

如果 `Policy` 和 `Code` 同时设置，`Policy` 优先。

### Mode

- `Disable`：无权限时禁用控件，并保留原始 `IsEnabled` 状态以便权限恢复后还原。
- `Hide`：无权限时将控件设为 `Collapsed`，并保留原始 `Visibility`。
- `Both`：无权限时同时禁用和隐藏。
- `Prompt`：保留给提示型场景；attached property 不直接弹窗。

### 指定局部权限服务

默认情况下，控件使用 `Permission.DefaultPermissionService`。如果某个区域需要独立权限上下文，可以给控件设置局部服务：

```csharp
Permission.SetService(deleteButton, permissionService);
Permission.SetPolicy(deleteButton, "User.Delete");
```

### 权限刷新

`Permission` 会订阅 `IPermissionService.PermissionsChanged`。当权限服务发出变更通知时，控件会自动重新计算 `IsEnabled` 和 `Visibility`。

```csharp
PermissionsChanged?.Invoke(this, new PermissionChangedEventArgs("User.Delete"));
```

传入空权限码表示全量刷新：

```csharp
PermissionsChanged?.Invoke(this, new PermissionChangedEventArgs());
```

## 推荐实践

- 控件权限、命令权限和业务授权共享同一个 `IPermissionService`。
- UI 层只负责禁用、隐藏、恢复原始状态，不承载权限规则。
- 关键业务操作仍应通过 `IAuthorizationService` 做最终校验。
- 不建议在 attached property 或控件中直接查询数据库。
- 登录、切换用户、权限刷新后，由权限服务统一触发 `PermissionsChanged`。

## 依赖

- `Kwy.MVVM`
- `Microsoft.Extensions.DependencyInjection`
- `.NET 8.0-windows+`
