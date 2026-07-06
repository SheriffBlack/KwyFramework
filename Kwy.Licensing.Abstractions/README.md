# Kwy.Licensing.Abstractions

`Kwy.Licensing.Abstractions` 定义 Kwy 框架的授权公共抽象。它不引用任何商业 SDK，也不绑定具体密码狗厂商。

授权分为两类：

- 第三方 SDK 激活：例如 HslCommunication、HALCON、Cimetrix。
- 软件/功能授权：例如整机软件需要密码狗才能运行，或某些功能需要授权后才能使用。

## SDK 激活

用于启动时激活第三方商业库。

- `ILicenseActivator`：单个 SDK 的激活器。
- `ILicenseActivationService`：统一执行所有 SDK 激活器。
- `LicenseActivationResult`：激活结果。

例如 `Kwy.Device.PLCs.Hsl` 中的 `HslCommunicationLicenseActivator` 会调用 HSL 的授权入口。

## 软件与功能授权

用于密码狗、本地授权文件、云授权等场景。

- `ILicenseProvider`：授权来源，例如密码狗、授权文件、云服务。
- `IFeatureLicenseService`：功能授权检查服务。
- `LicenseCheckContext`：授权检查上下文，包含应用、客户、机器和功能码。
- `LicenseCheckResult`：授权检查结果，包含功能列表和到期时间。
- `LicenseLostPolicy`：运行中授权丢失后的建议策略。

功能码建议使用稳定字符串：

```csharp
KwyLicenseFeatures.VisionEditor
KwyLicenseFeatures.SecsGem
KwyLicenseFeatures.MotionAdvanced
```

业务项目也可以定义自己的功能码，例如：

```csharp
public static class MyLicenseFeatures
{
    public const string RecipeAdvancedEdit = "Recipe.AdvancedEdit";
    public const string CameraStation4 = "Camera.Station4";
}
```

## 推荐分层

公共抽象放在本项目。

默认组合、缓存和功能检查实现放在 `Kwy.Licensing.Core`。

具体厂商实现放在独立模块：

- `Kwy.Licensing.Dongle.SenseLock`
- `Kwy.Licensing.Dongle.Rockey`
- `Kwy.Licensing.Dongle.SafeNet`
- `Kwy.Vision.Halcon`
- `Kwy.Communicate.Secs.Cimetrix`

这样设备层、通信层、视觉层和 UI 层都可以共享授权能力，但不会互相耦合。
