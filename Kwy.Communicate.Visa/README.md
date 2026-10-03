# Kwy.Communicate.Visa

`Kwy.Communicate.Visa` 是一个不依赖依赖注入容器的 VISA/GPIB 仪器通信库。当前实现使用
`NationalInstruments.Visa` 官方包，并要求 Windows 系统安装兼容版本的 NI-VISA Runtime；
应用程序无需直接引用 NI 程序集。

NuGet 包只提供托管 API，不包含 VISA 本机驱动。应用程序可以在未安装驱动的计算机上正常启动，
但无法建立真实的 VISA/GPIB 会话。UI 可在显示连接功能前进行检测：

```csharp
VisaRuntimeStatus status = VisaRuntime.CheckAvailability();
if (!status.IsAvailable)
{
    // 显示 status.Message，并禁用连接按钮或引导用户安装驱动。
}
```

未提前检测时，`ConnectAsync` 也会把底层的 `NativeVisaException` 转换为更明确的
`VisaRuntimeUnavailableException`。安装 NI-VISA Runtime 后才能使用 VISA；如果连接的是 NI GPIB
板卡、USB-GPIB 适配器等 NI GPIB 硬件，还需要安装 NI-488.2 驱动。驱动架构必须与应用程序
进程架构兼容。

对于常用的 GPIB 场景，UI 层应配置板卡号和仪器地址，而不是要求用户手动拼接 VISA 资源名称：

```csharp
var config = new GpibConfig
{
    BoardNumber = 0,
    PrimaryAddress = 23,
    SecondaryAddress = 0,
    Timeout = 5000
};

await using var instrument = new VisaCommunication(config);
await instrument.ConnectAsync();
string identification = await instrument.QueryAsync("*IDN?");
```

`GpibConfig` 会将上述配置转换成标准 VISA 资源名称 `GPIB0::23::INSTR`。只有高级配置或
非 GPIB VISA 资源才需要直接设置 `VisaConfig.ResourceName`。

可以按需发现本机 VISA 资源：

```csharp
IReadOnlyList<string> resources = VisaResourceDiscovery.Find();
```

可选的运行时通信工厂同样不依赖 Microsoft 依赖注入：

```csharp
ICommunicationFactory factory = new CommunicationFactoryBuilder()
    .RegisterVisa()
    .Build();
```
