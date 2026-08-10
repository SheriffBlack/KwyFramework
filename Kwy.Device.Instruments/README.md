# Kwy.Device.Instruments 设计说明

`Kwy.Device.Instruments` 是 Kwy 设备层中的通用仪表实现项目。它用于封装 DCR、LCR、极性、饱和、万用表等测试仪表的品牌驱动。

这一层的目标是：

- 每个品牌仪表保留自己的通信命令、参数模型和原始结果模型。
- 对上层 Flow/DataDeal 暴露统一能力接口，避免业务流程直接判断品牌。
- 让同一工位可以替换不同品牌仪表，而不需要修改流程逻辑。

## 分层关系

```text
Kwy.Device.Abstractions
└── Instrument
    ├── IInstrumentDevice
    ├── IMeasurementInstrument
    ├── InstrumentMeasurementResult
    ├── InstrumentMeasurementValue
    └── InstrumentMeasurementJudgment

Kwy.Device.Core
└── Instrument
    └── InstrumentBase

Kwy.Device.Instruments
├── Adex
│   ├── AdexDcr
│   ├── AdexDcrConfig
│   └── AdexDcrResult
└── Hioki
    ├── HiokiLcr
    ├── HiokiLcrConfig
    └── HiokiLcrResult
```

引用方向是：

```text
KwyTemplate.Flow
    -> Kwy.Device.Abstractions
    -> IMeasurementInstrument

Kwy.Device.Instruments
    -> Kwy.Device.Abstractions
    -> Kwy.Device.Core
```

Flow 层不应该直接依赖 `AdexDcrResult`、`HiokiLcrResult` 这类品牌结果模型。

## 为什么保留品牌 Result

不同仪表返回的数据格式、判定文本和附加信息不一样。例如：

- ADEX DCR 可能返回电阻值和 `GO/HI/LO` 文本。
- HIOKI LCR 可能返回多组参数值和数字判定码。
- 后续 Keysight、Chroma、Tonghui 等仪表还可能返回更多状态字段。

因此品牌驱动内部仍然保留自己的强类型结果：

```csharp
public sealed record AdexDcrResult(double Resistance, string Judgment, string RawText);

public sealed record HiokiLcrResult(IReadOnlyList<HiokiLcrValue> Values, string RawText);
```

这些结果适合用于：

- 驱动内部解析。
- 调试仪表协议。
- 品牌专属功能。
- 高级业务确实需要读取品牌扩展字段的场景。

## 为什么还需要通用测量结果

如果 Flow/DataDeal 直接写：

```csharp
switch (meter)
{
    case AdexDcr adex:
        AdexDcrResult result = await adex.ReadResultAsync();
        break;

    case HiokiLcr hioki:
        HiokiLcrResult result = await hioki.ReadResultAsync();
        break;
}
```

那么每新增一种仪表，DataDeal 都要改一次。后续 DCR、电感、极性、饱和等工位都会出现同样问题，业务层会越来越重。

所以在抽象层提供统一测量能力：

```csharp
public interface IMeasurementInstrument : IInstrumentDevice
{
    ValueTask<InstrumentMeasurementResult> ReadMeasurementAsync(CancellationToken cancellationToken = default);
}
```

通用结果：

```csharp
public sealed record InstrumentMeasurementResult(
    IReadOnlyList<InstrumentMeasurementValue> Values,
    string RawText);

public sealed record InstrumentMeasurementValue(
    string Name,
    double Value,
    InstrumentMeasurementJudgment Judgment,
    string? RawValue = null,
    string? Unit = null);
```

这样 Flow/DataDeal 只依赖 `IMeasurementInstrument`，不关心当前仪表到底是 ADEX、HIOKI 还是后续其他品牌。

## 当前实现方式

### ADEX DCR

`AdexDcr` 保留：

```csharp
public ValueTask<string> ReadRawResultAsync(...)
public ValueTask<AdexDcrResult> ReadResultAsync(...)
```

同时实现通用能力：

```csharp
public async ValueTask<InstrumentMeasurementResult> ReadMeasurementAsync(...)
{
    AdexDcrResult result = await ReadResultAsync(...);
    return new InstrumentMeasurementResult(
        [new InstrumentMeasurementValue("DCR", result.Resistance, ...)],
        result.RawText);
}
```

### HIOKI LCR

`HiokiLcr` 保留：

```csharp
public ValueTask<string> ReadRawResultAsync(...)
public ValueTask<HiokiLcrResult> ReadResultAsync(...)
public ValueTask<HiokiLcrResult> ReadAllResultAsync(...)
```

同时实现通用能力：

```csharp
public async ValueTask<InstrumentMeasurementResult> ReadMeasurementAsync(...)
{
    HiokiLcrResult result = await ReadResultAsync(...);
    return new InstrumentMeasurementResult(values, result.RawText);
}
```

## Flow/DataDeal 应该怎么使用

推荐：

```csharp
public sealed class InstrumentMeasurementDataDeal : IStationDataDeal
{
    private readonly IMeasurementInstrument? meter;

    public InstrumentMeasurementDataDeal(string code, IMeasurementInstrument? meter)
    {
        this.meter = meter;
    }

    public async Task CollectAsync(...)
    {
        InstrumentMeasurementResult result = await meter.ReadMeasurementAsync(...);
        InstrumentMeasurementValue value = result.FirstValue();

        stationModel.TestValues["DCR"] = value.Value;
        stationModel.TestJudges["DCR"] = value.Judgment is InstrumentMeasurementJudgment.Ok
            or InstrumentMeasurementJudgment.Unknown;
    }
}
```

不推荐：

```csharp
case AdexDcr adex:
case HiokiLcr hioki:
```

除非这个 DataDeal 本身就是某个品牌的专用流程。

## Device 层怎么配合

`KwyTemplate.Device` 可以根据 Selection 配置或 GPIB 自动识别创建具体仪表：

```text
Instrument.Dcr.01 -> AdexDcr
Instrument.Dcr.01 -> HiokiLcr
```

但 Flow 层只取稳定角色 ID 对应的通用能力：

```csharp
Devices.TryGet<IMeasurementInstrument>(DeviceIds.Instrument("Dcr", 1), out var dcr)
```

这样同一工位换仪表时，只改设备配置，不改工艺流程。

## 新增仪表的建议流程

新增一个品牌仪表时，建议按下面步骤：

1. 新建品牌目录，例如 `Keysight`、`Tonghui`、`Chroma`。
2. 创建仪表类并继承 `InstrumentBase`。
3. 保留品牌专属配置类和品牌专属 Result。
4. 实现合适的通用能力接口，例如 `IMeasurementInstrument`。
5. 在 `ReadMeasurementAsync()` 中把品牌 Result 转为 `InstrumentMeasurementResult`。
6. 业务 Flow/DataDeal 继续依赖通用接口。

如果后续出现更明确的能力，可以继续扩展抽象接口，例如：

```text
IDcrMeterInstrument
ILcrMeterInstrument
IPolarityInstrument
ISaturationInstrument
```

但当前阶段先使用 `IMeasurementInstrument` 足够轻量。

## 设计取舍

当前设计不是把所有品牌结果强行抹平，而是分两层：

```text
品牌 Result：保留完整厂商语义，便于驱动调试和高级扩展。
通用 Result：提供 Flow/DataDeal 所需的稳定测量语义。
```

这样可以同时保证：

- 驱动层不丢失厂商细节。
- 流程层不被品牌类型污染。
- 新增仪表时，主要改驱动和设备装配，不改工艺代码。
