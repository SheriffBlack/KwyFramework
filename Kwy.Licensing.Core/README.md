# Kwy.Licensing.Core

`Kwy.Licensing.Core` 提供软件授权与功能授权的默认实现。

它不绑定任何具体密码狗厂商，只负责：

- 组合多个 `ILicenseProvider`。
- 缓存短时间内的授权检查结果。
- 判断某个功能码是否可用。
- 定义运行中授权丢失时的策略。

## 注册

```csharp
services.AddKwyLicensing(options =>
{
    options.CacheDuration = TimeSpan.FromSeconds(5);
    options.LicenseLostPolicy = LicenseLostPolicy.DisableNewOperations;
});
```

具体密码狗模块只需要注册自己的 `ILicenseProvider`：

```csharp
services.AddSingleton<ILicenseProvider, MyDongleLicenseProvider>();
```

## 检查功能授权

```csharp
var context = new LicenseCheckContext(
    ApplicationId: "KwyTemplate",
    CustomerId: "CustomerA",
    MachineId: "Machine001");

bool enabled = await featureLicenseService.IsFeatureEnabledAsync(
    KwyLicenseFeatures.VisionEditor,
    context);
```

建议将功能码放在业务项目自己的常量类中。`KwyLicenseFeatures` 只提供 Kwy 常见功能码示例。
