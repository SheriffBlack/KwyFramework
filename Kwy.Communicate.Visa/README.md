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
板卡、USB-GPIB 适配器等 NI GPIB 硬件，还需要安装 NI-488.2 驱动，并确认设备已在
NI MAX 或 Windows 设备管理器中正常识别。仅存在 `visa64.dll`/`nivisa64.dll` 不代表
GPIB 驱动已安装。驱动架构必须与应用程序进程架构兼容。

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

`GpibConfig` 与传统 NI-488.2 API 一样使用板卡号、主地址和副地址配置，但生成
`GPIB0::1::INSTR` 不代表本机真实存在该资源。应用启动阶段可先无异常检测：

```csharp
VisaResourceAvailabilityResult availability =
    VisaResourceDiscovery.CheckAvailability(config.ResourceName);

if (!availability.IsAvailable)
{
    // 记录 availability.Message，跳过该设备自动连接，继续启动应用。
}
```

资源检测用于可选的启动自动识别；显式的 `ConnectAsync` 仍会在连接失败时抛出明确异常，
以避免上层把“未连接”误判为“连接成功”。

库会区分 VISA Runtime 不可用、GPIB 等接口驱动不可用和指定资源不存在，
分别通过检测结果或异常类型反映，不再统一误报为 Runtime 缺失。

可选的运行时通信工厂同样不依赖 Microsoft 依赖注入：

```csharp
ICommunicationFactory factory = new CommunicationFactoryBuilder()
    .RegisterVisa()
    .Build();
```
