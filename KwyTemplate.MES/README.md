# KwyTemplate.MES.Abstract 设计说明

`KwyTemplate.MES.Abstract` 是模板设备项目的 MES 业务契约层。它只定义设备侧稳定可依赖的 MES 能力与数据模型，不关心客户 DLL、TCP/HTTP 报文、字段名和错误码。

客户实现项目，例如 `KwyTemplate.MES.Cyntec`，负责把真实客户 MES 协议、SDK、DLL 返回值映射成这里的统一模型。

## 设计目标

- 设备 App、Flow 不直接依赖客户 MES DLL 或客户报文字段。
- MES 按“设备业务能力”拆分，而不是做一个巨大的客户 API。
- 不同客户可以只实现自己支持的能力。
- 设备必须理解的数据使用强类型模型，例如测试上下限、标准值、测试结果。
- 客户差异很大的字段放入 `MesParameterBag`，由具体 Machine 或参数应用器解释。
- 保留原始 MES 回执和外部数据来源，方便追踪、日志和售后排查。

## 能力接口

| 接口 | 说明 |
| --- | --- |
| `IMesConnection` | MES 连接、断开、在线状态。 |
| `IMesWorkOrderService` | 根据工单向 MES 查询机台需要设定的数据。 |
| `IMesTrackService` | 进站、出站。 |
| `IMesStandardSampleService` | 标准件参数获取、标准件点检结果保存。 |
| `IMesReelService` | Reel 扫码 MES 交互。 |
| `IMesMachineStatusService` | 机台状态上传。 |
| `IMesResultUploadService` | 生产测试结果上传。 |

客户实现按需实现接口。例如：

```text
CyntecMesService
  IMesConnection
  IMesWorkOrderService
  IMesTrackService
  IMesStandardSampleService
  IMesReelService

MajieMesService
  IMesConnection
  IMesWorkOrderService
  IMesMachineStatusService
```

这样不会出现一堆“不支持但必须实现”的空方法。

## 推荐依赖方向

```text
KwyTemplate.App / KwyTemplate.Flow
          |
          v
KwyTemplate.MES.Abstract
          ^
          |
KwyTemplate.MES.Cyntec / KwyTemplate.MES.Xxx
```

App 和 Flow 只依赖 Abstract。客户项目只负责实现和注册能力接口。

## 工单拉参主流程

多数客户现场从工单开始：

```text
工单字符串
  -> IMesWorkOrderService.GetWorkOrderSetupAsync(...)
  -> MesWorkOrderSetup
  -> 当前 Machine 应用工艺参数、测试上下限、标准值
```

`MesWorkOrderSetup` 包含：

- 工单、产品、配方标识
- `MesParameterBag`：客户差异化工艺参数，例如前空格、包装数、Reel 规则
- `IReadOnlyList<MesMeasurementLimit>`：测试项上下限和标准值
- `MesExternalDataSource`：可选，记录真实数据来源，例如客户生成的本地文件

## 参数策略

MES 客户字段差异很大，所以这里分两层：

1. 强类型模型：设备必须理解的内容，例如上下限、标准值、测试结果。
2. 弹性参数包：客户特有内容，例如工艺标志、预留字段、Reel 规则。

强类型测试限值：

```csharp
public sealed record MesMeasurementLimit(
    string ParameterId,
    string DisplayName,
    double? LowerLimit,
    double? UpperLimit,
    double? StandardValue,
    string? Unit = null);
```

弹性参数：

```csharp
var bag = new MesParameterBag();
bag.Set("FrontBlank", 20, "前空格");
bag.Set("PackageQuantity", 5000, "包装数量");
```

## 回执与外部数据源

部分客户 MES 的接口并不会直接返回完整工艺参数，而是先返回类似报文回执：

```text
ReturnCode = 0 表示 MES 已接受/生成数据
ReturnMessage 表示 MES 返回说明
真实参数随后从约定路径读取，例如 D:\MES\Setup\{工单}.txt 或 D:\MES\Stdpart\{标准件号}.txt
```

这种模式不应该泄漏到 App 或 Flow。客户实现层应该完成两步：

1. 调用客户 MES API，判断 `ReturnCode == 0`。
2. 成功后读取约定文件，解析成 `MesWorkOrderSetup` 或 `MesStandardSampleSetup`。

Abstract 使用 `MesExchangeRecord` 保存原始回执，使用 `MesExternalDataSource` 记录数据来源：

```csharp
var source = new MesExternalDataSource(
    MesExternalDataSourceKind.File,
    @"D:\MES\Setup\WO123.txt",
    Format: "csv");

var exchange = new MesExchangeRecord(
    Operation: "woquery",
    ReturnCode: 0,
    ReturnMessage: "OK",
    RawResponse: rawMessage,
    DataSource: source);

return MesResult<MesWorkOrderSetup>.Ok(setup, exchange: exchange);
```

这样业务层仍然只消费统一模型，调试和日志又能追踪客户原始报文与文件路径。

## 标准值

MES 返回的标准值统一映射到 `MesMeasurementLimit.StandardValue`。

```csharp
new MesMeasurementLimit(
    ParameterId: "DCR1",
    DisplayName: "DCR1",
    LowerLimit: 9.8,
    UpperLimit: 10.2,
    StandardValue: 10.0,
    Unit: "ohm");
```

UI 图表可以使用：

- `LowerLimit` / `UpperLimit`：红色上下限线
- `StandardValue`：绿色标准值线

测试判定和标准件点检也可以使用同一份模型，避免 UI、Flow、MES 各维护一套限值。

## 客户实现边界

`KwyTemplate.MES.Cyntec`、未来 `KwyTemplate.MES.Xxx` 负责：

- 引用客户 DLL
- 调用客户 TCP/HTTP/SDK 接口
- 映射客户字段到 Abstract 模型
- 映射客户错误码到 `MesResult`
- 处理登录、心跳、重试、超时
- 解析客户约定的外部数据文件

`KwyTemplate.MES.Abstract` 不应该引用：

- WPF
- PLC
- Flow
- Device
- 客户 DLL
- 具体通信库

## 目录说明

```text
Events/
  MesStateChangedEventArgs.cs

Models/
  MesResult.cs
  MesExchangeRecord.cs
  MesRequestContext.cs
  MesParameterBag.cs
  MesMeasurementLimit.cs
  MesWorkOrderModels.cs
  MesTrackModels.cs
  MesStandardSampleModels.cs
  MesReelModels.cs
  MesMachineStatusModels.cs
  MesTestResultUploadRequest.cs

Services/
  IMesConnection.cs
  IMesWorkOrderService.cs
  IMesTrackService.cs
  IMesStandardSampleService.cs
  IMesReelService.cs
  IMesMachineStatusService.cs
  IMesResultUploadService.cs
```

## 使用建议

- App 或 Flow 按需依赖能力接口，而不是依赖具体客户实现类。
- Machine 负责解释 `MesParameterBag` 中的工艺参数，并应用到 PLC、仪表或 UI。
- 测试上下限和标准值尽量走 `MesMeasurementLimit`，不要散落在多个 ViewModel 中。
- 客户原始报文、ReturnCode、文件路径保留在 `MesResult.Exchange`，用于日志和排查。