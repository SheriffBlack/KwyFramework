# KwyTemplate.Device 设计说明

`KwyTemplate.Device` 是模板项目的设备装配层。它负责读取设备配置、创建设备实例，并注册到 `IDeviceRegistry`，供 `KwyTemplate.Flow` 按 `DeviceId` 使用。

这一层只回答：

- 当前机型需要哪些设备。
- 每个设备使用什么连接配置和参数配置。
- 如何把配置创建成真实设备对象。
- 如何注册到设备注册表。

它不写工艺流程，不写工位测试逻辑，也不直接刷新 UI。

## 当前分层

```text
KwyTemplate.Device
├── Devices
│   ├── DeviceDefinition.cs
│   ├── IMachineDeviceContext.cs
│   ├── IDeviceRegistryInitializer.cs
│   └── DeviceRegistryInitializer.cs
├── Profiles
│   ├── IDeviceCatalog.cs
│   ├── DefaultDeviceCatalog.cs
│   ├── DeviceSelectionConfig.cs
│   ├── DeviceDefinitionFactory.cs
│   ├── GpibInstrumentAutoDetector.cs
│   └── DeviceConfigProvider.cs
├── Plcs
│   ├── HslPlcDeviceDefinition.cs
│   └── Editors
│       └── HslPlcConfigEditorModel.cs
├── IoCards
│   └── AdvantechIoCardDeviceDefinition.cs
├── Instruments
│   ├── InstrumentDeviceDefinition.cs
│   ├── AdexDcrDeviceDefinition.cs
│   └── HiokiLcrDeviceDefinition.cs
├── Connections
│   └── Editors
│       ├── TcpConnectionEditorModel.cs
│       ├── SerialConnectionEditorModel.cs
│       └── GpibConnectionEditorModel.cs
├── DeviceIds.cs
└── DeviceModule.cs
```

## 主 PLC 默认连接

当前模板主 PLC 使用 HSL 的 `Keyence_NanoSerialOverTcp` 协议连接基恩士 KV8000，默认配置为：

```csharp
new HslPlcConfig
{
    Brand = HslPlcBrandType.Keyence_NanoSerialOverTcp,
    Transport = PlcConnectionTransport.Tcp,
    IpAddress = "192.168.0.10",
    Port = 8501,
    KeepAlive = false
}
```

配置会保存到：

```text
Config/{CatalogKey}/PLC.Main.json
```

如果现场 IP 不是 `192.168.0.10`，在系统配置页面修改 IP 后点击“应用”即可持久化。
## 核心概念

`IDeviceCatalog` 表示一套设备清单。它描述当前客户、当前机型默认需要创建哪些设备，例如主 PLC、IO 卡、DCR 仪表等。当前模板主 PLC 默认使用基恩士 Keyence KV8000 网口通信。

`DeviceConfigProvider` 表示设备配置来源。它按 `CatalogKey + DeviceId` 管理强类型配置，并持久化到 JSON。

`DeviceSelectionConfig` 表示同一套设备清单内的小设备选择项。例如 DCR 工位可以手动选择 ADEX 1152D 或 HIOKI 3542，也可以通过 GPIB 自动识别。

`DeviceDefinitionFactory` 根据选择结果创建具体的 `DeviceDefinition`。这样 `DefaultDeviceCatalog` 不会堆满品牌判断代码。

`GpibInstrumentAutoDetector` 是轻量自动识别器。它只负责用 `*IDN?` 判断 GPIB 地址上的仪表型号，不参与设备生命周期。

## Catalog 与 Selection 的取舍

推荐规则：

- 少量固定项目：一个 `Catalog` 写死即可。
- 不同客户或不同大机型：使用不同 `Catalog`。
- 同一客户同一机型下的小设备差异：不要拆多个 `Catalog`，使用 `DeviceSelectionConfig`。
- 组合变化多：一个 `Catalog` + `Selection` 配置 + `DeviceDefinitionFactory`。

例如四工位测试机里，只有 DCR 工位可选 ADEX 或 HIOKI，不建议拆成两套 Flow，也不建议复制两套 Catalog。保持设备角色 ID 稳定即可：

```csharp
DeviceIds.Instrument("Dcr", 1) // Instrument.Dcr.01
```

Flow/Machine 只关心 `Instrument.Dcr.01` 是一个 DCR 工位仪表，不关心具体品牌。

## 手动选择仪表

第一阶段使用手动选择：

```csharp
public sealed class DeviceSelectionConfig
{
    public DeviceSelectionMode DcrMeter1Mode { get; set; } = DeviceSelectionMode.Manual;

    public DcrMeterModel DcrMeter1 { get; set; } = DcrMeterModel.AdexDcr;
}
```

当 `DcrMeter1Mode = Manual` 时，`DefaultDeviceCatalog` 使用 `DcrMeter1` 创建对应仪表。

持久化文件示例：

```text
Config/Default/DeviceSelection.json
Config/Default/Instrument.Dcr.01.Serial.json
Config/Default/Instrument.Dcr.01.AdexDcr.json
Config/Default/Instrument.Dcr.01.HiokiLcr.json
```

## GPIB 自动识别

第二阶段支持轻量自动识别：

```csharp
public enum DeviceSelectionMode
{
    Manual,
    AutoDetect
}
```

当 `DcrMeter1Mode = AutoDetect` 时，`DefaultDeviceCatalog` 会读取：

```text
Config/Default/Instrument.Dcr.01.Gpib.json
```

然后通过 GPIB 发送：

```text
*IDN?
```

识别规则：

- 返回内容包含 `HIOKI` 或 `3542`，创建 HIOKI 3542。
- 返回内容包含 `ADEX`、`1152` 或 `AX1152`，创建 ADEX 1152D。
- 识别失败时，如果 `DcrMeter1FallbackToManual = true`，回退到手动选择型号。
- 如果不允许回退，则抛出明确异常，提示检查 GPIB 地址或改为手动选择。

默认 GPIB 配置：

```csharp
new GpibConfig
{
    BoardNumber = 0,
    PrimaryAddress = 23,
    Timeout = 3000,
    KeepAlive = false,
    AutoReconnect = false
}
```

对应资源名可以理解为：

```text
GPIB0::23::INSTR
```

## 配置路径

设备配置按 `CatalogKey` 隔离：

```text
Config/{CatalogKey}/{DeviceId}.json
```

默认示例：

```text
Config/Default/PLC.Main.json
Config/Default/DeviceSelection.json
Config/Default/Instrument.Dcr.01.Serial.json
Config/Default/Instrument.Dcr.01.Gpib.json
Config/Default/Instrument.Dcr.01.AdexDcr.json
Config/Default/Instrument.Dcr.01.HiokiLcr.json
```

## Flow 如何使用设备

`KwyTemplate.Flow` 不直接 new 设备，也不判断品牌。它通过 `IMachineDeviceContext` 或 `IDeviceRegistry` 获取稳定角色 ID 对应的设备：

```csharp
var dcr = Devices.GetRequired<IDevice>(DeviceIds.Instrument("Dcr", 1));
```

更推荐后续为 DCR 抽出统一能力接口，例如 `IDcrMeter`。这样 Flow 可以直接消费 DCR 能力，而不需要关心 ADEX/HIOKI 的具体类型。

## 新增设备建议

新增大机型：新增一个 `IDeviceCatalog`。

新增同一工位的小型号选择：扩展 `DeviceSelectionConfig` 和 `DeviceDefinitionFactory`。

新增连接配置 UI 元数据：放在 `Connections/Editors`。

新增设备参数 UI 元数据：优先放在对应设备模块或设备定义附近，避免业务层二次封装。


### 松下 FP-XH 串口备选配置

如果现场需要切回松下 FP-XH 串口通信，可以在 Catalog 中改用：

```csharp
HslPlcConfigDefaults.CreatePanasonicFpXhSerialPlc()
```

默认参数保留为：

```csharp
new HslPlcConfig
{
    Brand = HslPlcBrandType.Modbus_Rtu,
    Transport = PlcConnectionTransport.Serial,
    PortName = "COM6",
    BaudRate = 9600,
    DataBits = 8,
    Parity = ParityType.None,
    StopBits = StopBitsType.One,
    Station = 238,
    KeepAlive = false
}
```
