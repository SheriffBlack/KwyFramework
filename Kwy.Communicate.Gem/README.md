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
| 第 4 课：GEM 状态 | `GemCommunicationState`、`GemControlState`、`GemEquipmentService` | 决定何时允许 Online Remote |
| 第 5、11 课：S6F11、CEID/RPTID/VID | `GemRegistry`、`GemCollectionEvent`、`GemReport`、`GemVariable` | 建立稳定的事件数据字典 |
| 第 6 课：S2F41 Remote Command | `GemRemoteCommand`、`GemRegistry.RegisterCommand` | 校验 Control State 后调用应用命令 |
| 第 7 课：Alarm | `GemAlarm`、`ReportAlarmAsync` | 保证 Set/Clear 生命周期成对 |
| 第 8 课：SV/DV/EC/Trace | `GemVariableDefinition`、`GemEquipmentConstant`、`GemTraceService` | 提供一致的数据快照 |
| 第 9 课：Recipe/PPID | `GemRecipe`、`SaveRecipeAsync` | 接入项目配方服务、权限和版本校验 |
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

IGemEquipment equipment = new GemEquipmentService(
    client,
    registry,
    local: new GemEndpoint(
        GemHostRole.Equipment,
        "AOI-01",
        Model: "KWY-AOI",
        SoftwareRevision: "1.0.0"),
    remote: new GemEndpoint(GemHostRole.Host, "FAB-EAP"));

await equipment.EstablishCommunicationAsync(cancellationToken);
await equipment.SetOnlineAsync(remote: true, cancellationToken);
```

此时完成的是“连接 + S1F13/S1F14 + 本地 Control State 切换”。实际项目仍需按 Host
规范实现 Online/Offline 请求、通信恢复和状态转换策略。

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

### 4.2 启动时注册变量、报告和事件

```csharp
using Secs4Net;

registry.RegisterVariable(new GemVariable(
    EquipmentGemIds.LotIdVid,
    "LotId",
    Item.A(string.Empty)));

registry.RegisterVariable(new GemVariable(
    EquipmentGemIds.RecipeIdVid,
    "RecipeId",
    Item.A(string.Empty)));

registry.RegisterReport(new GemReport(
    EquipmentGemIds.LotStartedRptid,
    new[]
    {
        EquipmentGemIds.LotIdVid,
        EquipmentGemIds.RecipeIdVid
    }));

registry.RegisterEvent(new GemCollectionEvent(
    EquipmentGemIds.LotStartedCeid,
    "LotStarted",
    new[] { EquipmentGemIds.LotStartedRptid }));
```

建议只在启动阶段注册接口定义。生产过程中更新的是变量快照，不要并发修改整个接口表。

### 4.3 业务发生时更新快照并上报 S6F11

```csharp
public async Task ReportLotStartedAsync(
    string lotId,
    string recipeId,
    CancellationToken cancellationToken)
{
    registry.RegisterVariable(new GemVariable(
        EquipmentGemIds.LotIdVid,
        "LotId",
        Item.A(lotId)));

    registry.RegisterVariable(new GemVariable(
        EquipmentGemIds.RecipeIdVid,
        "RecipeId",
        Item.A(recipeId)));

    await equipment.ReportEventAsync(
        EquipmentGemIds.LotStartedCeid,
        cancellationToken);
}
```

需要保证同一事件的变量快照一致。复杂设备应在项目层增加锁、不可变快照或单线程事件队列，
避免 LotId 已更新而 RecipeId 仍是旧值。

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
Collection Event 上报。

## 6. 接收 Host Primary Message

`ISecsGemClient.GetPrimaryMessagesAsync()` 提供收到的 Primary Message。应用层应按 S/F 路由，
解析数据，调用业务命令，然后快速回复：

```csharp
await foreach (var primary in client.GetPrimaryMessagesAsync(cancellationToken))
{
    SecsMessage message = primary.PrimaryMessage;

    switch (message.S, message.F)
    {
        case (1, 1):
            using (var reply = GemMessageFactory.AreYouThereResponse())
            {
                await primary.TryReplyAsync(reply, cancellationToken);
            }
            break;

        case (2, 41):
            // 1. 解析 RCMD/CPNAME/CPVAL。
            // 2. 检查 Online Remote、设备状态、参数和安全互锁。
            // 3. 快速返回 S2F42，再由设备业务异步执行命令。
            await HandleRemoteCommandAsync(primary, cancellationToken);
            break;

        default:
            if (message.ReplyExpected)
            {
                // null 会由 Secs4Net 按未知消息处理为 S9F7。
                await primary.TryReplyAsync(null, cancellationToken);
            }
            break;
    }
}
```

实际项目建议建立 `(Stream, Function) → Handler` 路由表，不要让一个 `switch` 无限增长。
`TryReplyAsync()` 会沿用原 Primary Message 的 System Bytes，避免回复错配和 T3 Timeout。

当前 preview 提供消息接收入口和常用消息工厂，但不会自动把所有 Host 消息路由到设备业务；
具体命令、权限、安全互锁和 ACK 规则必须由设备项目实现。

## 7. Remote Command 与业务命令

可以在 `GemRegistry` 注册项目允许的命令：

```csharp
registry.RegisterCommand("START", async (command, token) =>
{
    if (equipment.ControlState != GemControlState.OnlineRemote)
    {
        return new GemRemoteCommandResult(
            GemAckCode.InvalidState,
            "Equipment is not Online Remote.");
    }

    // 调用 Equipment.Application 的命令总线，不要直接写 PLC。
    bool accepted = await machineCommands.StartAsync(token);

    return accepted
        ? new GemRemoteCommandResult(GemAckCode.Accepted)
        : new GemRemoteCommandResult(GemAckCode.Denied, "START was rejected.");
});
```

推荐链路：

```text
S2F41
  → 解析与协议校验
  → Online Remote / 状态 / 参数 / 权限校验
  → 应用命令总线
  → 快速 S2F42 ACK
  → 后续结果通过状态或 Collection Event 上报
```

“收到命令”不等于“业务执行完成”。耗时动作不应阻塞到超过 T3。

## 8. Recipe、Trace 与 Spooling

### Recipe

```csharp
var recipe = new GemRecipe(
    "RCP_A_V2",
    Item.A("recipe-body"),
    Version: "2");

await equipment.SaveRecipeAsync(recipe, cancellationToken);
```

`GemRegistry` 只提供内存模型和变更记录。配方文件、权限、签名、版本一致性和设备实际选用状态，
应接入具体项目的 Recipe Service。

### Trace

```csharp
var traceService = new GemTraceService(registry);

traceService.RegisterTrace(new GemTraceDefinition(
    TraceId: 1,
    SampleInterval: TimeSpan.FromSeconds(1),
    TotalSamples: 60,
    VariableIds: new[] { new GemVid(EquipmentGemIds.RecipeIdVid) }));

GemTraceSample sample = traceService.Capture(traceId: 1, sampleNumber: 1);
```

当前 `GemTraceService` 负责定义与采样快照，不负责调度定时器；采样调度、断线补传和数据持久化
由宿主或具体项目实现。

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
- Alarm、Event、Report、Variable、Equipment Constant、Recipe 和 Remote Command 模型。
- `GemRegistry`、Trace、内存 Spooling、History 和可替换诊断。
- Host/Equipment 角色上下文。

本包当前不提供：

- 完整、自动化的 E30 状态机和所有标准消息 Handler。
- 具体设备的 CEID/VID/RPTID/ALID/ECID 定义。
- PLC、运动控制、工艺流程和安全互锁。
- Recipe、History、Spooling 的生产级持久化。
- Host Simulator、SEMI 一致性认证或 Fab 验收脚本。
- E40/E87/E90/E94 业务实现；相关基础模型位于 `Kwy.Communicate.Gem300`。

先用配套教程理解概念，再以本 README 完成最小连接和业务映射，最后以客户 SEDD/接口表和
Host Simulator 驱动联调。不要把“能发送一条 S6F11”当作 GEM 项目完成；真正的交付目标是
状态、事件、命令、报警、配方和恢复流程与 Fab 语义保持一致。
