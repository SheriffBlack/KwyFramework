# Kwy.Communicate.Gem

`Kwy.Communicate.Gem` 是基于 Secs4Net 的 SEMI E30 GEM 行为层，通过
`CommunicationClientBase` 接入 Kwy 统一通信生命周期。

本包面向需要将设备业务接入 Fab Host/EAP 的开发者。它提供 HSMS/SECS 客户端、GEM
状态与数据模型、常用消息工厂、注册表、事件、报警、配方、Trace 和诊断扩展点，但不包含
具体设备的 CEID/VID/ALID 表、PLC 动作或工艺流程。

> 当前包以 `preview` 形式发布，公共 API 和互操作行为仍可能根据真实 Host/Equipment
> 联调反馈调整。本项目使用 MIT 许可证，但不代表通过 SEMI 认证。

## 1. 先建立正确分层

配套教程 `D:\Desktop\SECS_GEM学习.md` 讲解了从 HSMS、SECS-II、GEM 到
GEM300/EDA 的完整概念。对应到代码时，可以按下面四层理解：

```text
设备业务层
  设备状态、Lot、Recipe、Alarm、START/STOP
                    ↓ 显式映射
Kwy.Communicate.Gem
  CEID / RPTID / VID / ALID、Control State、消息语义
                    ↓
Secs4Net
  SECS-II Item、SxFy、System Bytes、Primary/Secondary 事务
                    ↓
HSMS / TCP
  Select、Linktest、连接和超时
```

源码目录按职责组织，但仍是一个 NuGet 包和一个程序集：

```text
Transport        HSMS/SECS 客户端与 Kwy 通信生命周期
Protocol         SxFy 定义、报文工厂、Parser 和 Handler
Routing          Primary Message 分发与标准 Handler 组装
Validation       会话策略与请求合同校验
Interface        客户 Profile、Catalog 和接口文档导出
Runtime          运行时快照、命令注册表与组合根
Session          Equipment 侧 GEM 会话编排
ProcessPrograms  Stream 7 与设备 Recipe Service 的存储边界
Trace/Spooling   采样与断线缓冲能力
Diagnostics      协议诊断与 Secs4Net 日志适配
Models           公共枚举和值对象
```

目录层次不会进入公共类型名；当前统一保持 `Kwy.Communicate.Gem`
命名空间，因此源码整理不会迫使现有项目修改 `using`。

几个容易混淆的概念：

- TCP 已连接不等于 HSMS 已 Selected，也不等于 GEM 已 Communicating。
- `System Bytes` 由 Secs4Net 管理；回复 Primary Message 时应使用
  `PrimaryMessageWrapper.TryReplyAsync()`，不要自己生成事务号。
- `GemCommunicationState` 和 `GemControlState` 属于协议状态；设备自身的
  Idle/Run/Fault 等状态属于具体项目业务。
- CEID、RPTID、VID、ALID 是设备接口合同，应来自客户确认的 SEDD/接口表，不应在运行时随机生成。

## 2. 教程与代码对照

| 教程内容 | 本库入口 | 实际项目要做的事情 |
|---|---|---|
| 第 1～3 课：SxFy、HSMS、SECS-II Item | `ISecsGemClient`、`SecsMessageId`、Secs4Net `Item` | 配置连接，理解 Primary/Secondary |
| 第 4 课：GEM 状态 | `GemCommunicationState`、`GemControlState`、`GemEquipmentSession` | 决定何时允许 Online Remote |
| 第 5、11 课：S6F11、CEID/RPTID/VID | `GemRegistry`、`GemCollectionEvent`、`GemReport`、`GemVariable` | 建立稳定的事件数据字典 |
| 第 6 课：S2F41 Remote Command | `GemRemoteCommand`、`GemRegistry.RegisterCommand` | 校验 Control State 后调用应用命令 |
| 第 7 课：Alarm | `GemAlarm`、`ReportAlarmAsync` | 保证 Set/Clear 生命周期成对 |
| 第 8 课：SV/DV/EC/Trace | `GemVariableDefinition`、`GemEquipmentConstant`、`GemTraceService` | 提供一致的数据快照 |
| 第 9 课：Recipe/PPID | `GemProcessProgram`、`IGemProcessProgramRepository` | 把 Stream 7 接入项目配方服务 |
| 第 12、24 课：GEM300 | `Kwy.Communicate.Gem300` | 仅 300 mm Carrier/Job 场景引入 |
| 第 13～23 课：联调、Simulator、现场 Bug | `IGemDiagnostics` 和具体项目测试工具 | 建立 Host Simulator 与接口一致性测试 |
| 第 25 课：EDA/Interface A | 不属于本包 | 使用独立的数据采集架构 |

## 3. 十分钟最小运行

### 3.1 创建客户端

本包采用 Factory，不要求 DI 容器：

```csharp
using Kwy.Communicate.Gem;

var config = new SecsGemClientConfig
{
    Host = "192.168.1.100",
    Port = 5000,
    DeviceId = 0,
    IsActive = true,
    T3Timeout = 45_000,
    KeepAlive = true,
    KeepAliveInterval = 60_000
};

await using ISecsGemClient client = SecsGemClientFactory.Create(config);
await client.ConnectAsync(cancellationToken);
```

`IsActive = true` 表示本端主动连接；被动模式应根据部署网络和 Host/EAP 约定配置。
客户端由 Factory 创建，因此调用方必须通过 `await using` 或 `DisposeAsync()` 释放。

### 3.2 建立 GEM 行为服务

```csharp
var registry = new GemRegistry();

IGemEquipmentSession equipment = new GemEquipmentSession(
    client,
    registry,
    local: new GemEndpoint(
        GemHostRole.Equipment,
        "AOI-01",
        Model: "KWY-AOI",
        SoftwareRevision: "1.0.0"),
    remote: new GemEndpoint(GemHostRole.Host, "FAB-EAP"));

// 先完成第 4、5、7 节的接口目录和 Remote Command Handler 注册，
// 再建立通信；建立通信时接口目录会被验证并封闭。
```

所有接口定义和 Handler 注册完成后，调用 `EstablishCommunicationAsync()` 完成
“连接 + S1F13/S1F14”，再由项目根据约定处理 Online 流程。`SetOnlineAsync()` 会进行
S1F1/S1F2 通信确认后切换设备侧 Control State，但不会代替 Host 发起的 S1F17/S1F18。

## 4. 把设备业务映射为 GEM

不要让 PLC、运动控制或工艺代码直接创建 `SecsMessage`。建议在具体设备项目中建立一个
映射层：

```text
Equipment.Application
  LotStarted / MachineFaulted / RecipeSelected
                         ↓
Equipment.Gem
  固定 ID、变量快照、命令校验
                         ↓
Kwy.Communicate.Gem
```

### 4.1 固定接口编号

```csharp
internal static class EquipmentGemIds
{
    public const uint LotIdVid = 1001;
    public const uint RecipeIdVid = 1002;

    public const uint LotStartedRptid = 2001;
    public const uint LotStartedCeid = 3001;

    public const uint EmergencyStopAlid = 4001;
}
```

编号一旦交付给 Host，不应因为代码重构而改变。

### 4.2 启动时构建设备接口目录

```csharp
using Secs4Net;

registry.Catalog.RegisterVariable(new GemVariableDefinition(
    new GemVid(EquipmentGemIds.LotIdVid),
    "LotId",
    GemVariableKind.DataVariable,
    Format: SecsFormat.ASCII));

registry.Catalog.RegisterVariable(new GemVariableDefinition(
    new GemVid(EquipmentGemIds.RecipeIdVid),
    "RecipeId",
    GemVariableKind.DataVariable,
    Format: SecsFormat.ASCII));

registry.Catalog.RegisterReport(new GemReportDefinition(
    new GemRptid(EquipmentGemIds.LotStartedRptid),
    new[]
    {
        new GemVid(EquipmentGemIds.LotIdVid),
        new GemVid(EquipmentGemIds.RecipeIdVid)
    }));

registry.Catalog.RegisterEvent(new GemCollectionEventDefinition(
    new GemCeid(EquipmentGemIds.LotStartedCeid),
    "LotStarted",
    new[] { new GemRptid(EquipmentGemIds.LotStartedRptid) }));

registry.Catalog.RegisterAlarm(new GemAlarmDefinition(
    new GemAlid(EquipmentGemIds.EmergencyStopAlid),
    "EMERGENCY_STOP",
    "Emergency stop is active",
    AlarmCode: 1));

```

`GemInterfaceCatalog` 表示设备向 Host 承诺的静态接口。验证会检查重复编号和名称、RPTID
引用的 VID、CEID 引用的 RPTID，以及重复关联。验证通过后目录会被封闭；
`EstablishCommunicationAsync()` 也会在通信前自动执行该检查。生产过程中只更新数据快照，
不能继续修改接口合同。

可以把同一份目录导出为 Markdown，纳入版本管理并交给客户评审：

```csharp
string interfaceDocument = GemInterfaceCatalogExporter.ToMarkdown(
    registry.Catalog,
    "AOI-01");
```

### 4.3 用 Profile 管理多客户接口

客户间只有 VID/CEID/RCMD 等接口合同不同时，可把定义保存为 JSON；真正的
机台动作仍使用经过编译和测试的 C# Handler。示例见
[`examples/gem-interface-profile.sample.json`](examples/gem-interface-profile.sample.json)。

```csharp
GemJsonInterfaceProfile profile =
    await GemInterfaceProfileLoader.LoadJsonAsync(profilePath, cancellationToken);

registry.ApplyProfile(profile);

// HandlerKey 是稳定的业务键；多个客户 RCMD 可映射到同一处理器。
registry.RegisterCommand("machine.start", async (command, token) =>
{
    string ppid = command.GetRequiredParameter("PPID").GetString();
    return await machineCommands.StartAsync(ppid, token);
});

registry.ValidateAndSeal();

GemInterfaceReleaseManifest release =
    registry.CreateReleaseManifest(softwareVersion: "2.3.4");

string customerDocument = GemInterfaceCatalogExporter.ToMarkdown(
    registry,
    "AOI-01",
    release.SoftwareVersion);
```

JSON 加载器支持枚举名称、注释和尾随逗号；加载时会在临时 Catalog 中检查
VID/RPTID/CEID 引用，并记录原文件 SHA-256。发布清单将软件版本、Profile ID、
接口版本和文件摘要绑定，便于 Fab 验收和现场追溯。JSON 不支持类型名、反射或
动态代码，因此更换 Profile 不会绕过已编译的安全互锁。

如果合同必须随软件编译和审查，可继承 `GemInterfaceProfile` 并在 `Configure()` 中使用
强类型 C# 注册相同定义。两种方式最终都进入同一 `GemInterfaceCatalog`、同一校验器
和同一 Markdown 导出器。Excel 编辑稿建议在项目工具层从 Catalog 生成，不在通信核心引入
Excel/OpenXML 依赖；经审核的 JSON Profile 才是生产时输入。

### 4.4 业务发生时更新快照并上报 S6F11

```csharp
public async Task ReportLotStartedAsync(
    string lotId,
    string recipeId,
    CancellationToken cancellationToken)
{
    registry.Data.SetVariables(new[]
    {
        new GemVariable(EquipmentGemIds.LotIdVid, "LotId", Item.A(lotId)),
        new GemVariable(EquipmentGemIds.RecipeIdVid, "RecipeId", Item.A(recipeId))
    });

    await equipment.ReportEventAsync(
        EquipmentGemIds.LotStartedCeid,
        cancellationToken);
}
```

`GemDataSnapshot` 只表示构造 GEM 报文时看到的当前值。上报事件时，如果某个 VID 没有值，
或者值的 SECS-II Format 与接口定义不一致，库会明确失败，不再静默发送空字符串。

同一批 `SetVariables()` 更新和一次事件取值都具有原子快照语义，可避免 LotId 已更新而
RecipeId 仍是旧值。业务层仍应保证传入的一组值本身来自同一个设备状态版本。

## 5. Alarm 生命周期

报警必须有稳定 ALID，并保证 Set/Clear 成对：

```csharp
await equipment.ReportAlarmAsync(
    new GemAlarm(
        EquipmentGemIds.EmergencyStopAlid,
        "Emergency stop pressed",
        GemAlarmState.Set,
        AlarmCode: 1),
    cancellationToken);

// 故障确实恢复后再 Clear，同一个 ALID。
await equipment.ReportAlarmAsync(
    new GemAlarm(
        EquipmentGemIds.EmergencyStopAlid,
        "Emergency stop released",
        GemAlarmState.Clear,
        AlarmCode: 1),
    cancellationToken);
```

不要把一般生产事件全部当作 Alarm。Alarm 表示需要 Host 关注的异常状态，生产节点变化通常通过
Collection Event 上报。上报前必须在 `GemInterfaceCatalog` 中注册 ALID；未定义或被禁用的报警
不会发送。

## 6. 接收 Host Primary Message

`GemPrimaryMessageRouter` 按 `(Stream, Function)` 分发 Primary Message，并统一保证一次回复、
System Bytes 关联和回复对象释放。标准 Handler 已覆盖 S1F3、S2F15、S2F23、S2F41、
S5F3 和 S7F3：

```csharp
var router = new GemPrimaryMessageRouter();

GemStandardHandlerSet.RegisterEquipmentHandlers(
    router,
    registry,
    equipment,
    equipmentConstantWriter: async (changes, token) =>
    {
        // 写入真实设备/Recipe Service，完成范围、权限和安全互锁检查。
        return await equipmentConstants.ApplyAsync(changes, token);
    },
    alarmEnablementWriter: async (request, token) =>
    {
        return await alarmService.SetHostEnablementAsync(
            request.Alid.Value,
            request.Enabled,
            token);
    });

// 客户自定义 S/F 可继续 Register(IGemPrimaryMessageHandler)。
await router.RunAsync(client, cancellationToken);
```

每个 Handler 的固定链路是：

```text
SecsMessage
  → IGemMessageParser<T>
  → GemSessionPolicy + IGemValidator<T>
  → 设备业务/存储边界
  → SxF(y+1) ACK
```

如果只希望注册某些报文，也可单独组合：

```csharp
router
    .Register(new S1F3Handler(registry))
    .Register(new S2F41Handler(equipment));
```

`S2F15Handler` 和 `S5F3Handler` 要求显式传入业务 Writer，不会仅修改内存就向 Host
返回成功。`S7F3Handler` 通过会话配置的 `IGemProcessProgramRepository` 保存 PPID/PPBODY。
原规划中的 `GemRecipeValidator` 也统一更名为 `GemProcessProgramValidator`，避免把 GEM
Process Program 误当成设备的完整 Recipe 领域模型。

未注册的 W-Bit 消息会交由 Secs4Net 生成 S9F7。不带 W-Bit 的未知消息仅被忽略。
业务权限、PLC 互锁、配方审批和客户特殊 ACK 仍属于具体项目。

## 7. Remote Command 与业务命令

Remote Command 同样分成“Fab 接口合同”和“设备处理器”。先定义双方确认的 RCMD、参数类型和
允许状态，再绑定设备应用层实现：

```csharp
registry.Catalog.RegisterRemoteCommand(new GemRemoteCommandDefinition(
    "START",
    new[]
    {
        new GemRemoteCommandParameterDefinition("PPID", SecsFormat.ASCII),
        new GemRemoteCommandParameterDefinition("LOTID", SecsFormat.ASCII)
    },
    new HashSet<GemControlState>
    {
        GemControlState.OnlineRemote
    }));

registry.RegisterCommand("START", async (command, token) =>
{
    string ppid = command.GetRequiredParameter("PPID").GetString();
    string lotId = command.GetRequiredParameter("LOTID").GetString();

    // 参数格式和 Online Remote 已由合同校验；设备安全互锁仍由应用层负责。
    bool accepted = await machineCommands.StartAsync(ppid, lotId, token);

    return accepted
        ? new GemRemoteCommandResult(
            GemAckCode.Accepted,
            CompletionStatus: GemRemoteCommandCompletionStatus.Completed)
        : new GemRemoteCommandResult(
            GemAckCode.Denied,
            "START was rejected.",
            GemRemoteCommandCompletionStatus.Rejected);
});

// 所有 VID/ECID/RPTID/CEID/ALID/RCMD 和 Handler 注册完成后执行。
registry.ValidateAndSeal();
await equipment.EstablishCommunicationAsync(cancellationToken);
await equipment.SetOnlineAsync(remote: true, cancellationToken);
```

启动校验会拒绝“定义了 RCMD 却没有 Handler”以及“注册了 Handler 却没有 Fab 接口定义”。
运行时统一检查 Control State、必填参数、重复参数、未知参数和 SECS-II Format；具体设备的
Idle/Run/Fault、安全互锁、配方存在性和权限仍由应用层处理。

推荐链路：

```text
S2F41
  → 解析与协议校验
  → Online Remote / 状态 / 参数 / 权限校验
  → 应用命令总线
  → 快速 S2F42 ACK
  → 后续结果通过状态或 Collection Event 上报
```

“收到命令”不等于“业务执行完成”。S2F41 Handler 应先完成协议、状态和参数校验，快速返回
S2F42；随后再调用 `ExecuteRemoteCommandAsync()` 执行业务动作。注册的命令处理器必须在真实
动作结束后才返回，`CompletionStatus` 用于区分 Completed、Failed、Cancelled 和 Rejected。
耗时动作不应阻塞 S2F42 超过 T3，最终结果通常通过设备状态或 Collection Event 上报。

## 8. Process Program、Trace 与 Spooling

### Process Program 与设备 Recipe

```csharp
IGemProcessProgramRepository repository =
    new EquipmentRecipeProcessProgramAdapter(equipmentRecipeService);
var equipment = new GemEquipmentSession(
    client,
    registry,
    processProgramRepository: repository);

var processProgram = new GemProcessProgram(
    "RCP_A_V2",
    Item.A("recipe-body"),
    Version: "2");

GemProcessProgramSaveResult saveResult =
    await equipment.SaveProcessProgramAsync(
        processProgram,
        new GemProcessProgramSaveOptions(Overwrite: false),
        cancellationToken);

IReadOnlyList<string> ppids =
    await equipment.ListProcessProgramIdsAsync(cancellationToken);
```

`GemProcessProgram` 表示 GEM Stream 7 的 PPID/PPBODY，不等于设备完整的 Recipe 领域对象。
`IGemProcessProgramRepository` 提供 PPID 目录、查询、保存、显式覆盖和删除能力。会话默认使用
`UnsupportedGemProcessProgramRepository`，未配置设备存储时明确拒绝保存和删除，避免重启后
数据丢失。`InMemoryGemProcessProgramRepository` 仅适合学习、测试和 Simulator；生产项目应
实现适配器，把 Process Program 导入设备现有的 Recipe Service。配方解析、签名、审批、当前
选用状态、运行中禁止覆盖或删除等规则仍由设备项目负责。

### Trace

```csharp
var traceService = new GemTraceService(registry);

traceService.RegisterTrace(new GemTraceDefinition(
    TraceId: 1,
    SampleInterval: TimeSpan.FromSeconds(1),
    TotalSamples: 60,
    VariableIds: new[] { new GemVid(EquipmentGemIds.RecipeIdVid) }));

GemTraceSample sample = traceService.Capture(traceId: 1, sampleNumber: 1);

// 按定义每秒采样一次，共采样 60 次，并逐条发送 S6F1。
GemTraceRunResult result = await equipment.RunTraceAsync(1, cancellationToken);
```

`CaptureTraceAsync()` 只采集一个快照；`RunTraceAsync()` 按 `SampleInterval` 和
`TotalSamples` 调度采样并逐条发送 S6F1，取消时返回 `Cancelled` 结果。当前实现不在后台偷偷
启动任务，因此其生命周期由调用方明确控制。断线补传和 Trace 数据持久化仍由宿主或具体项目实现。

### Spooling

`GemSpoolingService` 当前是有界内存队列，不是断电可恢复的生产级持久化 Spool。若客户接口要求
重启恢复、确认后删除或严格顺序，应在项目层实现持久化和重传策略。

## 9. 诊断与现场联调

默认诊断输出到 `System.Diagnostics.Trace`，默认记录消息元数据但不记录正文：

```csharp
var diagnosticsOptions = new GemDiagnosticsOptions
{
    LogMessages = true,
    LogMessageContent = false,
    MaxMessageContentLength = 4096
};

await using ISecsGemClient client = SecsGemClientFactory.Create(
    config,
    diagnostics: new TraceGemDiagnostics(),
    diagnosticsOptions: diagnosticsOptions);
```

正式项目可实现 `IGemDiagnostics`，转发到自己的日志系统；本库不规定文件、数据库或日志框架。
报文正文可能包含配方、Lot、Wafer 或客户敏感数据，只有在受控联调环境中才建议开启。

现场排查优先确认：

1. TCP 是否连接、HSMS 是否 Selected。
2. S/F、W-Bit、System Bytes 是否正确。
3. Primary 是否在 T3 内回复。
4. CEID、RPTID、VID 和变量顺序是否与接口表一致。
5. Alarm Set 后是否发送同 ALID 的 Clear。
6. Local 状态是否错误执行了 Remote Command。
7. Recipe 名称、版本与设备实际运行版本是否一致。

## 10. 项目落地建议

建议在具体设备仓库中增加：

```text
Equipment.Domain
  设备状态、报警、配方、Lot、工艺规则

Equipment.Application
  命令总线、用例、状态快照

Equipment.Gem
  SEDD ID 表、S/F Handler、业务映射、ACK 策略

Equipment.HostSimulator.Tests
  Host 流程、超时、非法状态和接口一致性测试
```

最少应自动验证：

- 所有 CEID/VID/RPTID/ALID 唯一且非零。
- 每个 Report 引用的 VID 存在。
- 每个 Event 引用的 RPTID 存在。
- Alarm Set/Clear 使用相同 ALID。
- 每个要求回复的 Primary 都在 T3 内回复。
- Offline/Local 状态拒绝不允许的 Remote Command。
- 断线重连后状态和需要补传的数据符合客户约定。

## 11. 当前能力边界

本包当前提供：

- Factory-only 的 HSMS/SECS 客户端创建和统一通信生命周期。
- Secs4Net 原生 `SecsMessage`、`Item` 和 `PrimaryMessageWrapper`。
- 命名的 `SecsMessageId`、`SecsMessageDefinition` 与常用 `GemMessageFactory`。
- GEM Communication/Control State 基础模型。
- Alarm、Event、Report、Variable、Equipment Constant、Process Program 和 Remote Command 模型。
- 由 `GemInterfaceCatalog`、`GemDataSnapshot`、`GemCommandRegistry` 组成的 `GemRegistry`。
- 接口关系校验、目录封闭、SECS Format 检查和 Markdown 接口表导出。
- 可替换 Process Program Repository、可控 Trace 运行、有界内存 History、内存 Spooling 和可替换诊断。
- Host/Equipment 角色上下文。

本包当前不提供：

- 完整、自动化的 E30 状态机和所有标准消息 Handler。
- 具体设备的 CEID/VID/RPTID/ALID/ECID 定义。
- PLC、运动控制、工艺流程和安全互锁。
- 设备 Recipe、History、Spooling 的生产级持久化。
- Host Simulator、SEMI 一致性认证或 Fab 验收脚本。
- E40/E87/E90/E94 业务实现；相关基础模型位于 `Kwy.Communicate.Gem300`。

先用配套教程理解概念，再以本 README 完成最小连接和业务映射，最后以客户 SEDD/接口表和
Host Simulator 驱动联调。不要把“能发送一条 S6F11”当作 GEM 项目完成；真正的交付目标是
状态、事件、命令、报警、配方和恢复流程与 Fab 语义保持一致。
