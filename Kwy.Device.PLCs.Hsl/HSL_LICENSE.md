# HslCommunication 授权说明

`Kwy.Device.PLCs.Hsl` 当前内置 HslCommunication 授权码：

```csharp
Authorization.SetAuthorizationCode("e0397905-7455-4533-8c7a-3ec89b68b2a7");
```

授权属于进程级 SDK 能力，不属于某一个 PLC 连接参数，因此不放在 `HslPlcConfig` 中。

## 默认行为

创建 HSL PLC 客户端前，模块会自动执行一次默认授权。业务项目只需要正常注册和连接 PLC：

```csharp
services.AddKwyHslPlc(
    deviceId: "PLC.Main",
    deviceName: "主 PLC",
    configure: options =>
    {
        options.Brand = HslPlcBrandType.Modbus_Rtu;
        options.Transport = PlcConnectionTransport.Serial;
        options.PortName = "COM6";
        options.BaudRate = 9600;
        options.DataBits = 8;
        options.Station = 238;
    });
```

## 统一授权入口

如果应用启动阶段希望统一激活所有商业 SDK，也可以继续注册 HSL 激活器：

```csharp
services.AddHslCommunicationLicense();
```

或者覆盖默认授权码：

```csharp
services.AddHslCommunicationLicense(options =>
{
    options.LicenseKey = configuration["Licenses:HslCommunication"];
    options.Required = true;
});
```

然后在启动阶段统一执行：

```csharp
var activationService = serviceProvider.GetRequiredService<ILicenseActivationService>();
IReadOnlyList<LicenseActivationResult> results = await activationService.ActivateAllAsync();
```

## 设计约定

- `HslPlcConfig` 只描述 PLC 连接、协议、站号、超时等设备参数。
- HSL 授权逻辑集中在 `Licensing` 文件夹，不散落到 PLC 读写逻辑里。
- 默认授权保证 HSL PLC 模块开箱可用。
- 如果未来授权码更换，只需要调整 `HslCommunicationLicenseOptions.DefaultLicenseKey` 或在应用层覆盖 `LicenseKey`。
