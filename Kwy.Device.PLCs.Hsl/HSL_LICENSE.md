# HslCommunication 授权

HslCommunication 如果使用商业授权版本，授权激活属于进程级 SDK 能力，不属于某一台 PLC 的连接配置。因此激活码不要放进 `HslPlcConfig`，而是通过独立授权入口配置。

## 注册

```csharp
services.AddHslCommunicationLicense(options =>
{
    options.LicenseKey = configuration["Licenses:HslCommunication"];
    options.Required = true;
});
```

## 启动时激活

```csharp
var licenseService = serviceProvider.GetRequiredService<ILicenseActivationService>();
var results = await licenseService.ActivateAllAsync();

if (results.Any(result => !result.Success))
{
    throw new InvalidOperationException(results.First(result => !result.Success).Message);
}
```

## 实现说明

`HslCommunicationLicenseActivator` 会直接调用 HslCommunication 的授权入口：

```csharp
Authorization.SetAuthorizationCode(options.LicenseKey);
```

`ILicenseActivationService` 来自 `Kwy.Licensing.Abstractions`。未来 HALCON、Cimetrix、相机 SDK 等需要授权时，也可以注册各自的 `ILicenseActivator`，统一在应用启动阶段激活。
