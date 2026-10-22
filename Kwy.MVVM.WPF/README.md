# Kwy.MVVM.WPF

[![NuGet](https://img.shields.io/nuget/v/Kwy.MVVM.WPF.svg)](https://www.nuget.org/packages/Kwy.MVVM.WPF)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](../LICENSES/MIT.txt)

`Kwy.MVVM.WPF` 是 Kwy MVVM 的 WPF 平台实现。它在 `CommunityToolkit.Mvvm` 和 `Microsoft.Extensions.DependencyInjection` 的基础上，把 `Kwy.MVVM` 的核心契约接入 WPF 的依赖属性、视觉树、窗口和应用启动流程，面向模块化桌面应用提供导航、弹窗、鉴权和 UI 生命周期管理。

核心原则：

- `Kwy.MVVM` 保持 UI 无关。
- `Kwy.MVVM.WPF` 只处理 WPF 平台行为。
- 业务权限、导航、对话框等能力通过接口注入，不直接绑定具体业务实现。
- 页面和弹窗使用独立 DI 作用域，关闭或缓存回收时统一释放 ViewModel 及其依赖。

## 核心能力

### KwyApplication 与模块化

`KwyApplication` 统一组织 WPF 应用的服务注册、模块发现与依赖排序、容器构建、Shell 创建和应用退出清理。

模块在启动阶段完成服务注册；标记为按需的模块会延迟执行初始化逻辑。

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

导航页面在缓存期内保留独立 DI 作用域；缓存过期、手动移除或应用退出时，作用域与 ViewModel 会被一并释放。

### DialogService

提供基于 `IDialogWindow` / `IDialogAware` 的对话框服务。业务侧通过服务打开弹窗，不需要直接调用 `Window.ShowDialog()`。

```csharp
services.AddTransient<IDialogWindow, MyDialogWindow>();
```

每次弹窗会话都拥有独立 DI 作用域，弹窗关闭后自动释放 View、ViewModel 及其 scoped/transient 依赖。

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

### Mode

- `Disable`：无权限时禁用控件，并保留原始 `IsEnabled` 状态以便权限恢复后还原。
- `Hide`：无权限时将控件设为 `Collapsed`，并保留原始 `Visibility`。
- `Both`：无权限时同时禁用和隐藏。
- `Prompt`：保留给提示型场景；attached property 不直接弹窗。

### 权限服务

控件统一使用应用启动时设置的 `Permission.DefaultPermissionService`，不支持在单个控件上替换服务。

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

## 版本与许可证

当前公共 API 仍处于 `0.x` 阶段，可能在次要版本中调整。生产项目建议锁定具体包版本。

本项目由 Kwy 发布，Copyright © 2026 Kwy，基于 [MIT License](../LICENSES/MIT.txt) 开源。
