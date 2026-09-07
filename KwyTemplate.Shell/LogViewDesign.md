# LogView 日志设计说明

## 1. 定位

LogView 是程序运行界面中的用户可见日志窗口。
它用于让操作员、现场工程师、调试人员看到关键运行事件。
它不是开发者异常堆栈窗口，也不是长期持久化日志文件。

LogView 应显示用户能理解、现场能处理的信息：

- 设备连接完成：主 PLC
- 设备连接失败：DCR 电阻仪 1
- PLC 写入失败：Int16 address DM6202
- 扫码枪读取超时
- MES 工单导入失败：工单 123456

LogView 不建议直接显示完整异常堆栈。
完整异常、堆栈、内部参数，应进入开发者日志。
当前已接入 Kwy.Logging.Serilog 写入开发者文件日志。


## 2. 日志分层

当前设计将日志分为两层。

| 层级 | 面向对象 | 职责 | 持久化 |
| --- | --- | --- | --- |
| LogView / KwyLogService | 用户、现场人员 | 实时显示关键事件摘要 | 当前为内存日志 |
| Serilog / ILogService | 开发者、售后工程师 | 记录完整异常和上下文 | 已写入文件 |

LogView 关注清晰、低噪声、可理解。
开发者日志关注完整、可追溯、可定位。

## 3. 当前数据链路

### 3.1 启动阶段

LoadView 本身不直接向 LogView 写日志。
启动阶段的日志来自 StartupProgressService。

```text
DeviceStartupConnector
    -> StartupProgressService.Report(...)
    -> App.xaml.cs / WireStartupProgressLog(...)
    -> KwyLogService.AddStartupProgress(...)
    -> LogView 显示
```

这种设计让 LoadView 和 LogView 不互相引用。
启动进度既能驱动加载界面，也能沉淀到日志页。

### 3.2 运行阶段

进入主界面后，不会再弹出 LoadView。
后台重连、PLC 写入失败、仪表参数下发、扫码枪失败等，走设备事件链路。

```text
DeviceBase / HslPlcDevice / InstrumentBase / SerialBarcodeScannerDevice
    -> RaiseOperationOccurred(...)
    -> DeviceRegistry.PublishDeviceOperationAsync(...)
    -> IEquipmentEventSink.PublishAsync(...)
    -> LogEquipmentEventSink
    -> KwyLogService.Add(...)
    -> LogView 显示
```

设备连接状态变化链路：

```text
DeviceBase.RaiseStateChanged(...)
    -> DeviceRegistry.PublishStateChangedAsync(...)
    -> IEquipmentEventSink.PublishAsync(...)
    -> LogEquipmentEventSink
    -> KwyLogService.Add(...)
    -> LogView 显示
```

## 4. 启动期与运行期边界

启动期设备连接会产生两类信息：

- StartupProgressService 的进度日志。
- DeviceStateChanged 的状态变化事件。

如果两者都写入 LogView，同一次连接会出现多条重复日志。
因此当前约定：

- 启动阶段连接日志由 StartupProgressService 负责。
- 运行阶段后台重连日志由 DeviceStateChanged 负责。
- LogEquipmentEventSink 使用 StartupProgressService.IsCompleted 区分阶段。

核心判断：

```csharp
if (!startupProgress.IsCompleted)
{
    return false;
}
```

启动未完成时，不记录设备状态变化，避免重复。
进入主界面后，后台重连成功会进入 LogView。

运行期重连成功示例：

```text
设备连接成功：主 PLC
```

## 5. 当前记录规则

### 5.1 启动连接

由 DeviceStartupConnector 连接 Catalog 中注册的设备。
通过 StartupProgressService 上报给 LoadView 和 LogView。

会记录：正在连接设备、设备连接完成、设备连接失败、设备连接异常。

### 5.2 运行期连接

运行期连接状态由 LogEquipmentEventSink 处理 DeviceStateChanged。
当前只记录启动完成后的 Connected 状态。
失败通常会伴随 DeviceError。
因此不额外记录 StateChanged -> Error，避免同一次失败写两条错误日志。

### 5.3 PLC

| 操作 | 进入 LogView | 说明 |
| --- | --- | --- |
| 读取成功 | 否 | 高频轮询，不能刷屏 |
| 读取失败 | 是 | 通信或地址异常需要暴露 |
| 写入成功 | 否 | 正常写点很多，不干扰用户 |
| 写入失败 | 是 | 会影响设备流程 |

PLC 某个业务点写失败，不应直接等价为 PLC 掉线。
如果 PLC 仍能读取 DM6502，说明连接还活着。
这类问题应记录为操作失败，而不是把整台 PLC 状态置为 Error。

### 5.4 仪表和扫码枪

| 操作 | 进入 LogView | 说明 |
| --- | --- | --- |
| 参数写入成功 | 是 | 低频关键动作，需要确认 |
| 参数写入失败 | 是 | 会影响测试或点检 |
| 读取成功 | 否 | 测试值读取可能频繁 |
| 读取失败 | 是 | 需要提示设备异常 |
| 触发成功 | 否 | 正常触发频繁，无需显示 |
| 触发失败 | 是 | 会影响测试流程 |

当前实现：

- InstrumentBase.ApplyConfigAsync 记录参数写入成功和失败。
- InstrumentBase.QueryAsync 只记录读取失败。
- InstrumentBase.ReadResponseAsync 只记录读取失败。
- InstrumentBase.WaitAndReadTriggeredResultAsync 只记录读取失败。
- InstrumentBase.TriggerAsync 只记录触发失败。
- SerialBarcodeScannerDevice.TriggerScanAsync 只记录触发失败。
- SerialBarcodeScannerDevice.WaitForCodeAsync 只记录读取失败或超时。

### 5.5 MES

MES 需要区分界面摘要和报文日志。
LogView 建议显示 MES 在线、工单导入、Reel 扫描、标准件查询等摘要结果。
MES 文件日志建议记录 oAPI.ToString、请求报文、返回报文和完整异常。

## 6. 去重策略

LogEquipmentEventSink 中有短时间去重：

```csharp
private static readonly TimeSpan DuplicateSuppressWindow = TimeSpan.FromSeconds(3);
```

当前策略：

- 只对 Error 及以上级别去重。
- 相同 Code + Source + Message 在 3 秒内只显示一次。

目的：

- 防止高频轮询失败刷爆 LogView。
- 保留第一次错误，方便现场看到问题。
- 不影响普通 Info 日志显示。


## 7. 内存缓存策略

LogView 是实时观察窗口，不承担 7*24 小时历史追溯职责。
如果 UI 集合无限追加，长时间运行后会持续占用内存，并且会增加界面刷新、滚动、虚拟化维护的压力。
因此当前将内存缓存上限收敛在 KwyLogService 内部，而不是分散到 View、ViewModel 或业务模块。

当前规则：

- KwyLogService 默认最多保留最近 2000 条日志。
- MaxCount 可配置，但最小值保护为 1，避免误配置导致集合异常。
- 新日志加入后会自动裁剪，删除 Sequence 最小的最旧日志。
- 裁剪按日志产生顺序处理，不按界面显示索引处理。
- 启动进度日志可能带 SortOrder 插入到前面，仍然不会影响“保留最近日志”的语义。
- ObservableCollection 的追加、插入、删除都统一回到 UI Dispatcher 执行。

对应代码：

```text
Kwy.UI.WPF.Components/Logging/KwyLogService.cs
```

长期追溯继续交给文件日志：

```text
Logs/yyyyMMdd.txt
Logs/Developer/yyyyMMdd.txt
```

这样设计后，7*24 小时运行时，LogView 的内存占用是有上限的；完整历史仍可通过文件日志查询。

## 8. 关键类职责

- StartupProgressService：记录启动项目、百分比，并提供 IsCompleted。
- KwyLogService：保存 UI 实时日志集合，为 LogView 提供数据源。
- LogEquipmentEventSink：筛选设备事件，格式化为用户可读日志，并做去重。
- DeviceRegistry：订阅设备事件，并发布到 IEquipmentEventSink。
- DeviceBase：统一设备生命周期和操作事件上报。

## 9. 为什么设备层不直接写 LogView

不建议设备直接依赖 KwyLogService 或 LogView。

原因：

- 设备层应与 UI 解耦。
- 设备库可能被控制台、服务、测试程序复用。
- 不同应用对日志展示要求不同。
- 设备层只需要描述发生了什么。
- Shell 层决定给谁看、怎么看、要不要去重。

当前设计可以概括为：

```text
设备层只描述发生了什么
Shell 层决定是否显示和如何显示
UI 层只负责展示
```

这样更符合低耦合、高内聚。

## 10. Serilog 持久化设计

当前 LogEquipmentEventSink 已同时写两条链路：

```text
用户摘要日志 -> KwyLogService -> LogView
开发者完整日志 -> ILogService / Serilog -> 文件
```

建议规则：

| 内容 | LogView | Serilog |
| --- | --- | --- |
| 用户可读摘要 | 是 | 是 |
| 完整异常堆栈 | 否 | 是 |
| 设备 ID、点位地址 | 可摘要 | 是 |
| MES 原始报文 | 否 | 是 |
| 高频读取成功 | 否 | 否 |
| 高频读取失败 | 是，去重 | 是，可限流 |

当前落地文件路径：

```text
Logs/yyyyMMdd.txt
Logs/Developer/yyyyMMdd.txt
Logs/Developer/yyyyMMdd.json
```

其中 `Logs/yyyyMMdd.txt` 面向用户和现场人员，记录摘要信息；`Logs/Developer/yyyyMMdd.txt` 和 `.json` 面向开发者，记录完整异常和上下文。

## 11. 开发注意事项

1. 不要在 ViewModel、Feature、Orchestrator 中到处直接调用 KwyLogService.Add 记录硬件日志。
2. 设备相关日志优先通过 OperationOccurred、DeviceRegistry、IEquipmentEventSink 链路。
3. 启动进度继续走 StartupProgressService。
4. 不要在设备连接策略里再写一套启动日志。
5. PLC 高频读取不要记录成功日志。
6. 轮询读取失败必须考虑去重或限流。
7. LogView 显示用户可读摘要。
8. 完整异常后续进入 Serilog。
9. 运行期后台重连不弹 LoadView，只进入 LogView。
10. KwyLogService 只作为实时 UI 缓冲区使用，不要依赖它保存历史日志。
11. 中文文档和源码统一使用 UTF-8 保存。

## 12. 总结

LogView 的定位是实时、用户可读、低噪声的运行日志窗口。
启动阶段由 StartupProgressService 负责。
运行阶段由设备事件链路负责。
设备层不直接依赖 UI。
Shell 层通过 LogEquipmentEventSink 统一筛选和格式化日志。

当前已在现有架构上接入 Kwy.Logging.Serilog，用于完整异常追溯。
让 LogView 保持清爽，让开发者日志保存完整上下文。

## 13. 当前落地配置

- Shell 通过 AddKwySerilogLogging 注册 ILogService。
- 普通用户摘要日志：Logs/yyyyMMdd.txt。
- 开发者日志：Logs/Developer/yyyyMMdd.txt 与 Logs/Developer/yyyyMMdd.json。
- 普通用户摘要日志保留 31 天。
- 开发者日志保留文件数量：31。
- 设备事件中的完整异常通过 Exception 属性写入 Serilog。
- App.xaml.cs 已记录 UI 未处理异常、未观察 Task 异常、AppDomain 未处理异常。
