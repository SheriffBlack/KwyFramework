# KwyTemplate.Flow 设计说明

`KwyTemplate.Flow` 是模板项目的业务流程层。它不负责创建设备，也不维护底层连接；设备由 `KwyTemplate.Device` 创建并注册到 `IDeviceRegistry`，Flow 层只按 `DeviceId` 获取设备并编排机台动作。

当前设计刻意保持接近旧项目 `Kwy.Flow.Workflows` 的直观写法：一个机型可以先集中写在一个 `MachineXXX.cs` 文件中。等机型复杂后，再按 PLC 点位、IO 点位、工站定义拆分 partial 或子文件夹。

## 分层关系

```text
KwyTemplate.Device
  创建设备 / 配置连接 / 注册 IDeviceRegistry
            ↓
  IMachineDeviceContext
  按 DeviceId 给 Flow 提供设备实例
            ↓
KwyTemplate.Flow
  MachineBase + 具体机型 + 工位 + DataDeal
            ↓
KwyTemplate.App
  绑定 Columns / Rows / Stations / Operations 显示 UI
```

## MachineBase 职责

`MachineBase` 只放通用机制，不放某台设备的具体点位。

```text
MachineBase
├── Start / Stop / Dispose
├── InitTestStations()
├── TestStations
├── PartColumns / PartRows
├── PLC 点位缓存
├── IO 卡引用
├── IO 快照缓存
├── IO 轮询循环
├── IsDiOn / IsRisingEdge / IsFallingEdge
├── ReadStationTrigger(station)
├── ReadStationResult(station)
├── CompleteStationHandshakeAsync(station)
├── OnTestStarted()
└── OnTestStopped()
```

不同客户、不同机型的 PLC 点位、IO 点位、仪表数量、工位数量都放在具体机型类中，例如 `MachineDemoPLC`。

## 工位三点 IO 握手

每个工位通过 `StationIoBinding` 绑定三个标准 IO 点：

```text
StationIoBinding
├── TestFinishedInput             测试完成输入，上升沿触发读取
├── ResultSource                  结果来源，默认 Hardware
├── ResultOkInput / ResultNgInput 硬件比较结果输入
├── ResultOkOutput / ResultNgOutput PC 软件判定结果输出
└── ResultReadCompletedOutput     PC 读数完成输出，通知 PLC/外部控制器
```

典型流程：

```text
PLC / IO 卡置位 TestFinishedInput
        ↓
MachineBase 统一刷新 IO 快照
        ↓
MachineBase 默认用 ReadStationTrigger(station) 判断上升沿
        ↓
CompositeDataDeal 执行该工位 DataDeal
        ↓
MachineBase 按 ResultSource 读取硬件结果，或由 DataDeal 给出软件判定
        ↓
MachineBase 更新 UI 表格与统计
        ↓
CompleteStationHandshakeAsync(station) 输出 ResultReadCompletedOutput 脉冲
```

这样每个工位只关心自己的语义点位，不直接反复读 IO 卡。

## 为什么 IO 由 MachineBase 集中扫描

旧式写法里，每个工位可能各自读 IO 卡点位。工位多、扫描周期短时，会造成重复读卡，而且不同工位看到的 IO 状态不一定来自同一个时间片。

现在的做法是：

```text
MachineBase
├── 周期读取 IO 卡一次，形成 DI 快照
├── 保存 previous / current 两份快照
├── 统一计算上升沿 / 下降沿
└── 工位只调用 ReadStationTrigger(station)
```

好处：

- IO 卡只读一次，减少驱动调用和总线压力。
- 所有工位使用同一份快照，时序一致。
- 上升沿、下降沿、去抖、报警扩展可以集中维护。
- `CompositeDataDeal` 不再直接读 IO 卡，只负责工站流程调度。

## TestStationModel

`TestStationModel` 描述一个工位需要什么，而不是直接创建设备。

```text
TestStationModel
├── StationId / StationName
├── IsEnabled
├── OrderedTestNames
├── TestValues / TestJudges
├── StationIo
├── StationDataDeals
├── Operations
└── ParallelDeals
```

设备实例由具体机型通过 `IMachineDeviceContext` 按 `DeviceId` 获取。工位模型本身不直接 `new` PLC、IO 卡或仪表。

## DataDeal 设计

`IStationDataDeal` 表示一次工站数据采集能力，例如 DCR、LCR、极性、视觉、扫码。

推荐职责边界：

```text
DataDeal
├── 读取或接收本次测试数据
├── 写入测试值
├── 判断 OK / NG
└── 写入 TestStationModel.TestValues / TestJudges
```

不建议在 `DataDeal` 中直接刷新 UI、写文件、写数据库或上传 MES。后续如果需要高频保存与汇总，建议增加结果管线：

```text
ResultPipeline
├── ResultPresenter：抽帧刷新 UI
├── StationResultSink：工位独立保存
├── ProductResultAggregator：整料汇总
└── BatchResultWriter：批量保存
```

这样可以避免“每测一次就写一次文件/数据库”的性能问题。

## CompositeDataDeal

`CompositeDataDeal` 是工位调度器：

```text
CompositeDataDeal
├── 等待 MachineBase.ReadStationTrigger(station)
├── 执行一个或多个 DataDeal
├── 读取 MachineBase.ReadStationResult(station)
├── 更新 MachineBase 的测试值、判定和统计
├── 投递 ProcessTestRecordAsync
└── 调用 CompleteStationHandshakeAsync 完成 IO 握手
```

它不直接读 IO 卡，也不关心 PLC/仪表从哪里创建。

## 工位操作能力

当前模板的工位操作只保留点检和校正。不要把这些按钮写死到 UI，统一用 `StationOperationDescriptor` 声明。

```csharp
Operations =
{
    new StationOperationDescriptor
    {
        Code = StationOperationDescriptor.Check,
        DisplayName = "点检"
    }
}
```

UI 根据 `station.Operations` 自动生成按钮。执行时调用：

```csharp
await machine.ExecuteStationOperationAsync(station, operationCode, token);
```

具体机型按需重写该方法即可。不需要点检/校正的机型，不写 Operation 即可。

## 新增机型建议

第一阶段推荐最简单方式：复制 `Machines/MachineDemoPLC.cs`，新增一个机型类，例如：

```text
Machines
  CustomerA_Model01.cs
```

在新机型中集中定义：

- PLC 点位枚举，使用 `[PlcPoint]` 和 `[Description]`。
- IO 输入/输出枚举，使用 `[Description]`。
- `InitTestStations()` 中定义工位、测试列、DataDeal 和 Operation。
- 默认 `ReadStationTrigger()` 使用 `StationIo.TestFinishedInput` 的上升沿；特殊机型才重写。
- 默认 `ReadStationResult()` 使用 `StationIo.ResultSource` 区分硬件/软件判定：Hardware 读取 `ResultOkInput/ResultNgInput`，Software 由 DataDeal 写入 `TestJudges`。
- 默认 `CompleteStationHandshakeAsync()` 会先按 `isPass` 写 `ResultOkOutput/ResultNgOutput`，再输出 `ResultReadCompletedOutput` 20ms 脉冲；特殊机型才重写。
- `OnTestStarted()` / `OnTestStopped()` 中定义启动/停止动作。

当一个机型文件过大时，再拆成：

```text
Machines
  CustomerA_Model01
    CustomerA_Model01.cs
    CustomerA_Model01PlcPoints.cs
    CustomerA_Model01IoPoints.cs
    CustomerA_Model01Stations.cs
```

不要一开始就过度拆分，避免模板变重。

## 设备获取方式

不要在工位或 DataDeal 中直接 `new` PLC、仪表、IO 卡。推荐在具体机型中使用：

```csharp
var plc = Devices.GetRequired<IPlcDevice>(DeviceIds.MainPlc);
var dcr = Devices.GetRequired<AdexDcr>(DeviceIds.Instrument("AdexDcr", 1));
```

设备创建、连接、释放交给 `KwyTemplate.Device`；Flow 只使用设备。





## 工位结果判定来源

`StationIoBinding.ResultSource` 用于区分 OK/NG 来自哪里，默认值是 `Hardware`，以兼容原有机型。

```csharp
StationIo = new StationIoBinding
{
    TestFinishedInput = (int)CardToPc.ResistanceReceivedFinished,
    ResultSource = StationResultSource.Hardware,
    ResultOkInput = (int)CardToPc.RsOk,
    ResultReadCompletedOutput = (int)PcToCard.ResistanceFinished
}
```

当结果由 PC 判断时，将 `ResultSource` 改为 `Software`。此时 DataDeal 根据仪表值、上下限、配方或补偿规则写入 `TestJudges`，Base 会根据最终 `isPass` 写 OK/NG 输出。

```csharp
StationIo = new StationIoBinding
{
    TestFinishedInput = (int)CardToPc.ResistanceReceivedFinished,
    ResultSource = StationResultSource.Software,
    ResultOkOutput = (int)PcToCard.ResistanceOk,
    ResultNgOutput = (int)PcToCard.ResistanceNg,
    ResultReadCompletedOutput = (int)PcToCard.ResistanceFinished
}
```


## 工位启用状态

`TestStationModel.IsEnabled` 表示该工位是否参与运行，默认值为 `true`。后续对接 MES、配方或机型配置时，可以通过该字段控制某个工位是否启用。

- `IsEnabled = true`：工位参与轮询触发、手动执行和结果统计。
- `IsEnabled = false`：工位不响应 IO 触发，也不会执行 DataDeal。

该字段只表达“工位是否启用”，不替代设备连接状态、报警状态或权限控制。
