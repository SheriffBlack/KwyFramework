# Kwy.Licensing.Abstractions

`Kwy.Licensing.Abstractions` 是 Kwy 框架的极薄授权契约包。

它只解决一件事：在应用启动阶段统一激活第三方商业 SDK，例如 HslCommunication、HALCON、Cimetrix、相机 SDK 等。

## 设计边界

本项目不做完整授权平台，不包含密码狗策略、功能授权、授权缓存、试用期、机器码、授权文件解析或 UI 弹窗。

如果某个业务项目需要密码狗或功能授权，建议在业务项目或独立扩展包中实现，不要把复杂策略放回基础抽象层。

## 保留类型

- `ILicenseActivator`：单个 SDK 或商业库的激活器。
- `LicenseActivationResult`：单次激活结果。
- `ILicenseActivationService`：统一执行所有激活器。
- `LicenseActivationService`：按顺序执行所有 `ILicenseActivator` 的默认实现。

## 使用示例

```csharp
services.TryAddSingleton<ILicenseActivationService, LicenseActivationService>();
services.TryAddEnumerable(ServiceDescriptor.Singleton<ILicenseActivator, MySdkLicenseActivator>());

var activationService = serviceProvider.GetRequiredService<ILicenseActivationService>();
IReadOnlyList<LicenseActivationResult> results = await activationService.ActivateAllAsync();
```

## 分层建议

具体 SDK 激活逻辑放在对应功能包里，例如：

- `Kwy.Device.PLCs.Hsl` 中实现 `HslCommunicationLicenseActivator`。
- 未来 `Kwy.Vision.Halcon` 可以实现 `HalconLicenseActivator`。
- 未来 Cimetrix 扩展包可以实现自己的激活器。

这样用户安装功能包时，NuGet 会自动带上本契约包；只有开发自定义授权器时，才需要直接引用 `Kwy.Licensing.Abstractions`。
