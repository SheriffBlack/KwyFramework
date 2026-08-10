# 工位结果判定设计

本文档说明 `KwyTemplate.Flow` 中 IO 判定、PC 判定与仪表解析之间的职责边界。

## 总体原则

工位测试结果分两层：

1. 仪表层负责通讯与解析。
   仪表驱动把原始报文解析成 `InstrumentMeasurementResult`，其中包含原始文本、干净数值、单位，以及仪表自身给出的 `Judgment`。
2. Flow 层负责工位判定与流程编排。
   Flow 不解析品牌协议，只根据 `StationIoBinding.ResultSource` 决定 OK/NG 来自 IO，还是来自 PC 软件规则。

也就是说，仪表只回答“我测到了什么”，Flow 决定“这个工位是否通过”。

## 关键模型

### StationIoBinding.ResultSource

`StationIoBinding.ResultSource` 是工位结果来源开关。

- `Hardware`：硬件/IO 判定，默认方式。
- `Software`：PC 软件判定。

默认使用 `Hardware`，这是为了兼容当前机台多数通过 PLC 或 IO 点位给出 OK/NG 的现场逻辑。

### TestStationModel.TestValues

`TestValues` 保存本次工位测试值。

Key 是测试项名称，例如：

```csharp
TestValues["DCR1"] = 23.6;
```

### TestStationModel.TestJudges

`TestJudges` 保存本次工位测试项判定。

Key 同样是测试项名称：

```csharp
TestJudges["DCR1"] = true;
```

### TestStationModel.TestLimits

`TestLimits` 保存 PC 软件判定所需的上下限。

它由机型在应用 MES 工单参数、配方参数或人工设置参数时写入：

```csharp
station.SetTestLimit("DCR1", lowerLimit, upperLimit, unit);
```

这样 `MeasurementJudgeService` 不需要知道 MES、界面或具体机型，只需要读取工位模型上的测试项上下限。

## IO 判定

IO 判定是当前默认模式。

典型配置：

```csharp
StationIo = new StationIoBinding
{
    TestFinishedInput = (int)CardToPc.DCR1测试完成,
    ResultSource = StationResultSource.Hardware,
    ResultOkInput = (int)CardToPc.DCR1_OK,
    ResultReadCompletedOutput = (int)PcToCard.DCR1读取完成
};
```

流程如下：

```text
PLC / IO 置位 TestFinishedInput
        ↓
MachineBase 统一扫描 IO 快照，并判断上升沿
        ↓
CompositeDataDeal 捕捉到工位触发
        ↓
ResultSource == Hardware
        ↓
MachineBase.ReadStationResult(station) 读取 ResultOkInput / ResultNgInput
        ↓
CompositeDataDeal 把该 IO 判定作为 triggerResult 传给 DataDeal
        ↓
InstrumentMeasurementDataDeal 读取仪表测值，并把 TestJudges[testName] = triggerResult
        ↓
MachineBase 更新 DataGrid、统计、握手输出
```

这种模式下，PC 不根据上下限重新判定 OK/NG。PC 只读取仪表值用于显示、记录和统计，最终 OK/NG 以现场 IO 点位为准。

## PC 判定

PC 判定用于这些场景：

- 现场没有提供 OK/NG IO 点位。
- 要由 PC 根据 MES 下发的上下限判定。
- 要由 PC 根据配方、补偿值或更复杂规则判定。

典型配置：

```csharp
StationIo = new StationIoBinding
{
    TestFinishedInput = (int)CardToPc.DCR1测试完成,
    ResultSource = StationResultSource.Software,
    ResultOkOutput = (int)PcToCard.DCR1_OK,
    ResultNgOutput = (int)PcToCard.DCR1_NG,
    ResultReadCompletedOutput = (int)PcToCard.DCR1读取完成
};
```

流程如下：

```text
PLC / IO 置位 TestFinishedInput
        ↓
MachineBase 统一扫描 IO 快照，并判断上升沿
        ↓
CompositeDataDeal 捕捉到工位触发
        ↓
ResultSource == Software
        ↓
CompositeDataDeal 不读取硬件 OK/NG，triggerResult 固定为 true
        ↓
InstrumentMeasurementDataDeal 读取仪表测值
        ↓
MeasurementJudgeService 根据 TestLimits 或仪表 Judgment 判定
        ↓
TestJudges[testName] = PC 判定结果
        ↓
MachineBase 更新 DataGrid、统计，并按最终 isPass 输出 OK/NG
```

## MeasurementJudgeService 判定规则

`MeasurementJudgeService` 是 PC 判定的默认实现。

当前规则：

1. 如果 `TestStationModel.TestLimits` 中存在该测试项上下限，优先使用上下限判定。
2. 上下限是闭区间，相等也算合格。
3. 如果没有上下限，则兼容旧逻辑，使用仪表层解析出的 `InstrumentMeasurementValue.Judgment`。

伪代码：

```csharp
if (station.TestLimits.TryGetValue(testName, out limit)
    && (limit.LowerLimit.HasValue || limit.UpperLimit.HasValue))
{
    return value >= lowerLimit && value <= upperLimit;
}

return value.Judgment is Ok or Unknown;
```

这里的 `Judgment` 不是 IO 判定，而是仪表驱动解析原始报文后得到的仪表自身判定。

如果某个仪表协议没有返回 OK/NG，则驱动可以返回 `Unknown`。在没有上下限的兼容场景下，`Unknown` 暂时不阻断；后续如果希望更严格，可以在 `MeasurementJudgeService` 中统一调整。

## 仪表层职责

仪表层的入口是：

```csharp
ValueTask<InstrumentMeasurementResult> ReadMeasurementAsync(CancellationToken cancellationToken = default);
```

仪表驱动内部可以有私有解析方法：

```csharp
private static InstrumentMeasurementResult ParseMeasurement(string rawText)
```

或：

```csharp
private static InstrumentMeasurementJudgment ParseJudgment(string rawText)
```

但不建议把 `ParseJudgmentAsync` 暴露成 Flow 需要调用的通用接口。Flow 层只应该面对统一结果：

```csharp
InstrumentMeasurementResult result = await meter.ReadMeasurementAsync(token);
InstrumentMeasurementValue value = result.Values[0];
```

这样可以避免 Flow 层知道 ADEX、HIOKI 或其他品牌仪表的协议细节。

## DataDeal 职责

`InstrumentMeasurementDataDeal` 当前职责：

- 触发仪表。
- 读取仪表测量结果。
- 取出指定 `ValueIndex` 的测值。
- 写入 `TestStationModel.TestValues`。
- 按 `ResultSource` 决定 `TestJudges` 来源。

简化逻辑：

```csharp
stationModel.TestValues[TestName] = value.Value;
stationModel.TestJudges[TestName] =
    stationModel.StationIo.ResultSource == StationResultSource.Hardware
        ? triggerResult
        : judgeService.IsPass(stationModel, TestName, value);
```

因此 DataDeal 不直接依赖 MES，也不直接依赖 UI。

## MES 上下限如何进入 PC 判定

MES 工单解析后会得到类似：

```csharp
MesWorkOrderInstrumentSetup
{
    ParameterId = "DCR1",
    LowerLimit = 19,
    UpperLimit = 25.5,
    Unit = "mΩ",
    Range = "1Ω"
}
```

具体机型负责把这些参数应用到设备和工位模型。

以 `Machine_2_A` 为例：

```csharp
MesWorkOrderInstrumentSetup? dcr1Setup = FindInstrumentSetup(instrumentSetups, "DCR1");

SetStationTestLimit(
    "DCR1",
    dcr1Setup?.LowerLimit,
    dcr1Setup?.UpperLimit,
    dcr1Setup?.Unit);

await ApplyAdexDcrSetupAsync(dcrMeter1, dcr1Setup, cancellationToken);
```

这里分成两件事：

- `SetStationTestLimit(...)`：给 PC 判定服务使用。
- `ApplyAdexDcrSetupAsync(...)`：给真实仪表写入参数使用。

这两个动作都放在机型中，因为只有机型知道 MES 参数与具体工位、具体仪表之间的对应关系。

## 什么时候选择 IO 判定

推荐使用 IO 判定的场景：

- PLC 或仪表硬件已经稳定输出 OK/NG。
- 现场原机台逻辑就是通过 OK/NG 输入点位判定。
- PC 只需要显示测值，不希望改变机台原有判定结果。

此时 `ResultSource` 保持默认 `Hardware` 即可。

## 什么时候选择 PC 判定

推荐使用 PC 判定的场景：

- MES 下发上下限，要求 PC 统一判定。
- 同一个测值需要根据不同工单、配方、补偿参数动态判定。
- 工站没有硬件 OK/NG 点位。
- 后续需要把判定规则统一收敛到软件服务中。

此时将工站配置为：

```csharp
ResultSource = StationResultSource.Software
```

同时确保机型在合适时机调用：

```csharp
SetStationTestLimit(testName, lowerLimit, upperLimit, unit);
```

## 扩展建议

后续如果 PC 判定越来越复杂，不建议把规则继续写进 `InstrumentMeasurementDataDeal`。

可以扩展 `IMeasurementJudgeService`：

- 按测试项选择不同判定策略。
- 支持标准值加偏差判定。
- 支持多值组合判定。
- 支持客户专属判定规则。
- 支持把判定过程和失败原因返回给 UI 或日志。

推荐演进方向：

```text
IMeasurementJudgeService
        ↓
DefaultMeasurementJudgeService
        ↓
CustomerMeasurementJudgeService
        ↓
CompositeMeasurementJudgeService
```

但在当前阶段，默认服务只做“上下限优先，仪表 Judgment 兜底”，保持简单即可。

## 仪表配置如何进入 PC 判定

`TestStationModel.TestLimits` 不只来自 MES。对于支持上下限配置的仪表，仪表驱动也可以把当前配置中的上下限暴露给 Flow。

为此在设备抽象层增加了 `IMeasurementLimitProvider`：

```csharp
public interface IMeasurementLimitProvider
{
    bool TryGetMeasurementLimit(out InstrumentMeasurementLimit limit);
}
```

链路如下：

```text
PC 参数界面 / JSON 配置 / MES 工单
        ↓
AdexDcrConfig.LowerLimitRaw / UpperLimitRaw / Range
        ↓
AdexDcr.TryGetMeasurementLimit(...)
        ↓
InstrumentMeasurementDataDeal.RefreshStationLimitFromInstrumentConfig(...)
        ↓
TestStationModel.TestLimits[testName]
        ↓
MeasurementJudgeService
```

这样有两个好处：

- MES 下发参数时，参数写入仪表 Config 后，会自然进入 PC 判定。
- 没有 MES 工单时，用户在 PC 参数界面设置的上下限，也能进入 PC 判定。

注意，Flow 不直接读取 `AdexDcrConfig.LowerLimitRaw`。因为 `LowerLimitRaw` 和 `UpperLimitRaw` 是 ADEX 命令原始值，不一定等于测量值的工程单位。

ADEX 的 raw 值换算由 `AdexDcr` 自己完成：

```text
AdexDcrConfig raw value + Range
        ↓
AdexDcr.ConvertRawLimitToEngineeringValue(...)
        ↓
InstrumentMeasurementLimit
```

这保证 Flow 层拿到的是“可直接和仪表测量值比较”的上下限，而不是某个品牌仪表的协议原始值。

因此现在的关系不是单纯的 MES 双写：

```text
MES -> Machine -> TestLimits
MES -> Machine -> AdexDcrConfig
```

而是更完整的链路：

```text
MES / PC 设置 / JSON 配置
        ↓
仪表 Config
        ↓
仪表能力接口 IMeasurementLimitProvider
        ↓
Flow TestLimits
        ↓
PC 判定
```

MES 应用工单时仍然可以直接调用 `SetStationTestLimit(...)`，这是为了让非配置型仪表、PLC 参数或特殊工艺参数也能参与 PC 判定；但对于 ADEX 这类已经有上下限 Config 的仪表，最终会以仪表当前配置暴露出来的 limit 为准。