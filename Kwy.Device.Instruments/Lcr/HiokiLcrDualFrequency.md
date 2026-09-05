# HIOKI LCR 双频配置设计

## 目标

`HiokiLcrConfig` 是 HIOKI 3533、3570、3536 等 LCR 仪表共用的参数模型。双频不是所有型号、所有工位都具备的能力，因此不能通过仪表型号字符串或页面名称判断；由设备定义明确声明能力。

这样可做到：

- 3533 仍使用原有单频页面；
- 3570 电感工位可选择单频或双频；
- 后续支持双频的 3536 只增加能力声明，不复制配置模型或 UI；
- 不支持双频的设备即使收到包含 `LEnable2` 的工单，也不会展示或启用第二频。

## 能力声明

在机型的设备清单中创建 HIOKI LCR 定义时传入能力：

```csharp
CreateHiokiLcr(
    "Ind",
    index,
    $"HIOKI 3570 LCR {index}",
    primaryAddress,
    CreateDefaultIndHiokiLcrConfig,
    supportsDualFrequency: true);
```

`HiokiLcrDeviceDefinition` 会将该值写入运行时配置 `HiokiLcrConfig.SupportsDualFrequency`。

该属性标记为 `JsonIgnore`：它是设备能力，不属于客户工单或参数文件内容。

## 第一频与第二频

第一频继续使用既有字段：

| 内容 | 字段 |
| --- | --- |
| 负载类型 | `LoadType` |
| 频率 | `Frequency` / `FrequencyUnit` |
| 主测试项限值 | `Parameter1Min/Max` 与单位 |
| 副测试项限值 | `Parameter3Min/Max` 与单位 |

第二频使用独立字段，不覆盖第一频：

| 内容 | 字段 |
| --- | --- |
| 模式 | `FrequencyMode`：`单频` / `双频` |
| 频率 | `Frequency2` / `Frequency2Unit` |
| 负载类型 | `SecondLoadType` |
| 主测试项限值 | `SecondParameter1Min/Max` 与单位 |
| 副测试项限值 | `SecondParameter3Min/Max` 与单位 |

单双频是同一套界面的两种参数组，不是第一频界面后面再追加第二频界面：

- 选择“单频”时，显示并写入第一频字段；
- 选择“双频”时，显示并写入第二频字段；
- 双频页面保留与单频相同的基础设置、Ls、Rs/Q 分组结构。负载类型、频率、上下限与单位绑定第二频字段；电压、延迟、量程、速度显示第一频的共同设置，但不可编辑。

`HiokiLcrConfig.GetActiveMeasurementSettings()` 是唯一的模式选择出口；仪表写入、读值名称、软件判定上下限都读取该对象，避免各层分别判断单双频。

## 关闭工位：OFF-OFF

负载类型新增 `OFF-OFF`，它解析为参数对 `OFF` / `OFF`：

- 仪表写入 `:PARameter1 OFF`、`:PARameter3 OFF`，并关闭比较器；
- 不生成软件判定上下限；
- 离线工单保存时，该工位原有的测量项会标记为未启用；
- 极性、Q、第二频是否启用不再维护一套额外的本地 ToggleButton，而是由已启用的测量项自动推导为兼容的 MES 标记。

Machine_4_HAHH 的两台极性仪表属于同一个极性工艺组：在 SetView 将任一台切换为 `OFF-OFF` 时，两台都会同步为 `OFF-OFF`，配方生成 `ZEnable=N`，并关闭 PLC 的工位一、工位二；重新选择 `Z-θ` 时两台同步启用。该同步由机型的 `IInstrumentConfigurationGroupMachine` 能力处理，不放入本地工单的独立开关。

## QEnable 规则

- 单频且 `QEnable=Y`：维持历史行为，第一频为 `Ls-Q`。
- 双频且 `QEnable=Y`：第一频为 `Ls-Rs`，第二频强制为 `Ls-Q`。
- 双频且 `QEnable=N`：第二频为 `Ls-Rs`，可编辑其负载类型。

`SecondFrequencyQEnabled` 和 `IsSecondLoadTypeLocked` 是运行时状态，不写入 JSON；它们仅用于将第二频负载类型锁定为 `Ls-Q`。

## Cyntec MES 字段映射

| MES 字段 | 作用 |
| --- | --- |
| `LEnable2` | 是否启用第二频 |
| `LFreq2` | 第二频频率，Cyntec 单位为 kHz；程序显示为 MHz，仪表写入时转换为 Hz |
| `LMinValue2` / `LMaxValue2` / `LUnit2` | 第二频 Ls 限值与单位 |
| `RSMinValue2` / `RSMaxValue2` / `RSUnit2` | 第二频 Rs 限值与单位 |
| `QMinValue2` / `QMaxValue2` | 第二频 Q 限值；未提供时兼容回退至第一组 Q 限值 |
| `LCRRange2` | 第二频量程来源；当前第二频继承第一频量程，仅保留字段供后续协议扩展 |

解析后使用独立测试项 ID：`Ls2`、`Rs2`、`Q2`，避免与第一频 `Ls`、`Rs`、`Q` 混淆。

## 属性网格的通用条件元数据

为避免在 HIOKI 页面中写可见性判断，属性网格支持两个通用特性：

```csharp
[VisibleWhen(nameof(IsDualFrequencyEnabled))]
[DisableWhen(nameof(IsSecondLoadTypeLocked))]
```

- `VisibleWhen`：指定的布尔属性为 `true` 时显示。
- `DisableWhen`：指定的布尔属性为 `true` 时禁用编辑器。

它们位于 `Kwy.ComponentModel`，不依赖 HIOKI，其他设备的条件参数也可复用。

## 当前硬件写入与测量边界

HIOKI 在任一时刻只能运行一组面板参数。`HiokiLcr.JoinCommand()` 统一从 `GetActiveMeasurementSettings()` 取值：

- 当前为单频时，向仪表写入第一频的负载类型、频率和上下限；
- 当前为双频时，向仪表写入第二频的负载类型、频率和上下限；
- 电压、延迟、量程、速度是两种模式共享的仪表条件，双频页面仅只读展示。

因此“选择双频”表示仪表当前按第二频参数工作，并不是一次 PLC 触发自动连续测两次频率。若现场后续要求每颗料同时取得第一频和第二频结果，需要单独定义 PLC/PC 的两次触发时序及结果命名，再在工位采集链路中实现；不能把两组配置命令连续写入同一次测试中。
