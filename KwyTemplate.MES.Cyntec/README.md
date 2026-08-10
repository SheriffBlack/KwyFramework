# KwyTemplate.MES.Cyntec 设计说明

`KwyTemplate.MES.Cyntec` 是 Cyntec 客户 MES 适配项目。它实现 `KwyTemplate.MES.Abstract` 中的能力接口，并负责调用 Cyntec 客户 DLL、判断 `returncode`、读取客户约定的 D 盘文件，再转换成 Abstract 的统一模型。

业务层不应该直接引用 Cyntec DLL，也不应该知道 `D:\MES` 文件格式。

## 依赖

当前项目引用客户 DLL：

```text
DLL/cyntec.Base.dll
DLL/cyntec.TcpTools.dll
DLL/cyntec.Tools.dll
```

由于客户 DLL 可能是 32 位组件，项目设置了：

```xml
<PlatformTarget>x86</PlatformTarget>
```

## 实现类

核心实现：

```text
MesCyntecService.cs
```

辅助类型：

```text
CyntecMesOptions.cs      Cyntec 连接和文件路径配置
CyntecMesFileParser.cs  解析 D:\MES\Setup 与 D:\MES\Stdpart 文件
```

## 方法映射

| Abstract 方法 | Cyntec API | 说明 |
| --- | --- | --- |
| `ConnectAsync` | `mesAPIconnect` | MES 连线。 |
| `GetWorkOrderSetupAsync` | `mesAPIwoQuery` | 工单查询；成功后读取 Setup 文件。 |
| `TrackInAsync` | `mesAPIcheckIn` | 进站。 |
| `TrackOutAsync` | `mesAPIcheckOut` | 出站，使用 `OutputQuantity`。 |
| `GetStandardSampleAsync` | `mesAPISTDPartsQuery` | 标准件查询；成功后读取 Stdpart 文件。 |
| `SaveStandardSampleCheckAsync` | `mesStdPartsCheckResultSave` | 标准件点检结果保存。 |
| `ScanReelAsync` | `mesAPIReelQuery` | Reel 或条码查询。 |
| `DisconnectAsync` | 无客户 API | 当前只切换本地状态。 |

## ReturnCode 规则

Cyntec API 返回对象中通常包含：

```text
returncode
returnmessage
```

适配层统一按以下规则处理：

```text
returncode == 0  成功
returncode != 0  失败，转成 MesResult.Fail(...)
```

原始调用信息保存在 `MesResult.Exchange`：

```csharp
MesExchangeRecord
  Operation
  ReturnCode
  ReturnMessage
  TransactionId
  RawRequest
  RawResponse
  DataSource
```

这样 UI 或日志系统可以追踪客户原始回执。

## 工单查询流程

`GetWorkOrderSetupAsync` 的流程：

```text
1. 调用 mesAPIwoQuery
2. 判断 returncode 是否为 0
3. 成功后读取 D:\MES\Setup\{工单号}.txt
4. 解析为 MesWorkOrderSetup
5. 返回 MesResult<MesWorkOrderSetup>.Ok(...)
```

默认路径由 `CyntecMesOptions` 控制：

```csharp
public string SetupDirectory { get; set; } = @"D:\MES\Setup";
public string SetupFileExtension { get; set; } = ".txt";
```

Setup 文件按旧项目规则解析为 `MesParameterBag`：

```text
第 2 列：参数 Key
第 3 列：参数 Value
```

典型参数会进入 `MesParameterBag`，例如：

```text
PackageQty
BeforeSpaceQty
AfterSpaceQty
BlankQty
SampleQty
TablePaperMatNo
TopCoverMatNo
MarkPrintString
```

测试上下限会尽量转换成 `MesMeasurementLimit`，例如：

```text
DCRMinValue / DCRMaxValue / DCRStandardValue
LMinValue   / LMaxValue   / LStandardValue
RSMinValue  / RSMaxValue  / RSStandardValue
```

## 标准件查询流程

`GetStandardSampleAsync` 的流程：

```text
1. 调用 mesAPISTDPartsQuery
2. 判断 returncode 是否为 0
3. 成功后读取 D:\MES\Stdpart\{标准件号}.txt
4. 解析为 MesStandardSampleSetup
5. 返回 MesResult<MesStandardSampleSetup>.Ok(...)
```

默认路径由 `CyntecMesOptions` 控制：

```csharp
public string StandardPartDirectory { get; set; } = @"D:\MES\Stdpart";
public string StandardPartFileExtension { get; set; } = ".txt";
```

标准件文件按旧项目规则解析，关键列如下：

```text
parts[6]  测试项，例如 DCR / LCR / RS
parts[7]  标准值
parts[8]  上限
parts[9]  下限
parts[10] 单位
parts[11] 频率
parts[12] 频率单位
parts[15] 标准件号
parts[16] 描述
```

这些数据会转换为：

```csharp
MesMeasurementLimit(
    ParameterId,
    DisplayName,
    LowerLimit,
    UpperLimit,
    StandardValue,
    Unit)
```

## 文件数据源追踪

当适配层读取外部文件时，会把文件路径写入：

```csharp
MesResult.Exchange.DataSource
MesWorkOrderSetup.DataSource
MesStandardSampleSetup.DataSource
```

这样后续日志中可以知道参数来自哪个文件，例如：

```text
D:\MES\Setup\9120011993.txt
D:\MES\Stdpart\Std0009.txt
```

## 配置示例

```csharp
var options = new CyntecMesOptions
{
    IpAddress = "127.0.0.1",
    Port = 13000,
    SetupDirectory = @"D:\MES\Setup",
    StandardPartDirectory = @"D:\MES\Stdpart"
};

var mes = new MesCyntecService(options);
```

## 注意事项

- 当前实现只负责 MES 适配，不直接写 PLC、不直接改仪表参数。
- Machine 或参数应用器应消费 `MesWorkOrderSetup`，再把参数应用到设备。
- 如果客户 DLL 后续升级为 64 位，可以移除 `PlatformTarget=x86`。
- 如果客户文件格式变化，只需要修改 `CyntecMesFileParser`，不影响 App / Flow。