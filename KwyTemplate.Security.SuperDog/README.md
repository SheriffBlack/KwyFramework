# KwyTemplate.Security.SuperDog

`KwyTemplate.Security.SuperDog` 是金雅特 / SuperDog 密码狗授权适配层。

## 设计边界

- `KwyTemplate.Security` 保留登录、账号、权限和 `ISecurityKeyChecker` 抽象。
- 本项目只负责调用 SuperDog 厂商库，并实现 `ISecurityKeyChecker`。
- Shell 或客户项目决定是否加载 `SuperDogSecurityModule`。不加载时，`KwyTemplate.Security` 会继续使用默认的 `NullSecurityKeyChecker`。

## DLL 依赖

当前只复制旧项目明确使用的 DLL：

- `dog_net_windows.dll`：SuperDog .NET 托管封装，编译引用。
- `api_dsp_windows.dll`：32 位运行时依赖。
- `api_dsp_windows_x64.dll`：64 位运行时依赖。
- `dog_windows_3164560.dll`：32 位厂商库。
- `dog_windows_x64_3164560.dll`：64 位厂商库。

未复制 `dogdnert*.dll` 和 `dog_windows*_demo.dll`，因为旧项目没有引用这些文件。

## 启用方式

在 Shell 的模块目录中加载：

`````csharp
moduleCatalog.AddModule<SecurityModule>();
moduleCatalog.AddModule<SuperDogSecurityModule>();
```

`SuperDogSecurityModule` 会替换 `ISecurityKeyChecker`，但不会影响程序启动和普通登录。需要密码狗保护的功能，在执行前主动检查授权。

## 默认授权

默认使用旧项目一致的：

- `FeatureId = 1`
- `Scope = "<dogscope />"`
- `VendorCode = SuperDogVendorCode.Code`

如后续客户 FeatureId 或 VendorCode 不同，可在 DI 中提前注册 `SuperDogOptions` 覆盖默认值。

## 使用建议

密码狗用于保护指定功能，不用于限制程序启动或普通登录。例如某个导出、标定、维护方法需要授权时，再在该方法入口检查：

```csharp
if (!securityKeyChecker.IsPresent())
{
    // 弹窗提示未检测到授权密码狗，并中断该功能。
    return;
}
``$([Environment]::NewLine)