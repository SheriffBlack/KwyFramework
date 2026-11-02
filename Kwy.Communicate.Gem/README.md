# Kwy.Communicate.Gem

`Kwy.Communicate.Gem` 是面向 **Equipment 侧**的 SEMI E30 GEM 行为编排库。它基于
[Secs4Net](https://github.com/mkjeff/secs4net) 处理 SECS-II/HSMS 事务，并接入
`Kwy.Communicate` 统一客户端生命周期。

> 当前为 `preview` 版本。本包不是 Fab Host，提供的是 E30 基础状态编排而非全部 E30 行为，也不代表
> 已通过 SEMI 认证。实际交付必须以客户 SEDD/接口表和 Host Simulator 验收结果为准。

## 安装

```xml
<PackageReference Include="Kwy.Communicate.Gem" Version="0.1.0-preview.1" />
```

- 目标框架：.NET 8
- 协议引擎：Secs4Net 3.0.1
- 创建方式：Factory-only，不强制使用 DI
- 默认角色：Equipment

## 核心能力

- HSMS/SECS 客户端创建与 `Kwy.Communicate` 生命周期。
- GEM Communication State 和 Control State 基础编排。
- VID、ECID、RPTID、CEID、ALID 与 Remote Command 接口合同。
- JSON/C# Profile、启动校验、目录封闭和 Markdown 接口文档导出。
- Primary Message Router、Parser、Validator 和可组合 Handler。
- Event、Alarm、Equipment Constant、Remote Command、Process Program 和 Trace 基础行为。
- 可替换诊断、Process Program Repository、Spool Store 和安全补传流程。

## 最小接入

### 1. 创建客户端与会话

```csharp
using Kwy.Communicate.Gem;
using Secs4Net;

CancellationToken cancellationToken = default;

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

GemInterfaceCatalog catalog = new GemInterfaceCatalogBuilder()
    .AddVariable(new GemVariableDefinition(
        new GemVid(1001),
        "LotId",
        GemVariableKind.DataVariable,
        Format: SecsFormat.ASCII))
    .AddReport(new GemReportDefinition(
        new GemRptid(2001),
        [new GemVid(1001)]))
    .AddEvent(new GemCollectionEventDefinition(
        new GemCeid(3001),
        "LotStarted",
        [new GemRptid(2001)]))
    .Build();

var registry = new GemRegistry(catalog);

using IGemEquipmentSession equipment = new GemEquipmentSession(
    client,
    registry,
    local: new GemEndpoint(
        GemHostRole.Equipment,
        "AOI-01",
        Model: "KWY-AOI",
        SoftwareRevision: "1.0.0"),
    remote: new GemEndpoint(GemHostRole.Host, "FAB-EAP"));
```

`SecsGemClient` 实现 `IAsyncDisposable`，必须使用 `await using` 或 `DisposeAsync()` 释放。
`IsActive = true` 表示本端主动建立 HSMS 连接。

### 2. 定义接口合同

```csharp
await equipment.EstablishCommunicationAsync(cancellationToken);
await equipment.SetOnlineAsync(remote: true, cancellationToken);
```

接口编号应来自已评审的 SEDD/客户接口表。Builder 在 `Build()` 时校验引用关系，
生成的 Catalog 不可变；运行期只更新数据快照。

`GemSessionStateMachine` 集中维护 Communication/Control State。底层连接离开 Connected
状态时，会话自动回退到 `NotCommunicating + Offline`。可以订阅状态变化：

```csharp
equipment.StateMachine.StateChanged += (_, change) =>
    Console.WriteLine(
        $"GEM: {change.PreviousCommunicationState}/{change.PreviousControlState} " +
        $"-> {change.CommunicationState}/{change.ControlState}; {change.Reason}");
```

### 3. 更新快照并上报事件

```csharp
using Item lotId = Item.A("LOT-001");
registry.Data.SetVariable(new GemVariable(1001, "LotId", lotId));
await equipment.ReportEventAsync(3001, cancellationToken);
```

上报时会检查 CEID/RPTID/VID 引用、必需快照值和 SECS-II Format。
`GemDataSnapshot` 不接管传入的 `Item`；调用方必须保证其在快照使用期间有效。
报文构造时会复制值，释放发送报文不会破坏快照。

### 4. 接收 Host Primary Message

```csharp
var router = new GemPrimaryMessageRouter();
GemStandardHandlerSet.RegisterEquipmentHandlers(
    router,
    registry,
    equipment,
    equipmentConstantWriter: WriteEquipmentConstantsAsync,
    alarmEnablementWriter: ApplyAlarmEnablementAsync,
    onlineRequestAuthorizer: CanAcceptOnlineRequestAsync,
    hostOnlineEntersRemote: false);

await router.RunAsync(client, cancellationToken);
```

Router 按 `(Stream, Function)` 分发消息，并统一处理 System Bytes 关联、一次回复和
回复对象释放。标准集合当前包含 S1F13、S1F17、S1F3、S2F15、S2F23、S2F41、S5F3、
S7F1、S7F3、S7F5、S7F17 和 S7F19。S1F17 的授权委托应检查设备安全状态、操作员权限和生产策略；默认进入
Online Local，只有项目明确授权时才应配置为直接进入 Online Remote。未注册的 W-Bit
消息由 Secs4Net 按未知报文处理。

## Remote Command

Remote Command 由接口合同和编译后的业务 Handler 组成：
命令定义必须在构建 Catalog 时添加，业务 Handler 则注册到 Registry。

```csharp
GemInterfaceCatalog catalog = new GemInterfaceCatalogBuilder()
    .AddRemoteCommand(new GemRemoteCommandDefinition(
        "START",
        [new GemRemoteCommandParameterDefinition("PPID", SecsFormat.ASCII)],
        new HashSet<GemControlState> { GemControlState.OnlineRemote },
        HandlerKey: "machine.start"))
    .Build();

var registry = new GemRegistry(catalog);

registry.RegisterCommand("machine.start", (command, token) =>
{
    // 拷贝成应用层自有数据，不得把收到的 SECS Item 长期入队。
    string ppid = command.GetRequiredParameter("PPID").GetString();
    bool queued = applicationCommands.TryQueueStart(ppid);

    return Task.FromResult(new GemRemoteCommandResult(
        queued
            ? GemHostCommandAckCode.Accepted
            : GemHostCommandAckCode.CannotPerformNow));
});
```

`GemHostCommandAckCode.Accepted` 对应 HCACK=4，表示命令已受理、完成结果稍后通过事件上报。
只有在 S2F42 前已完成动作时，才返回 `GemHostCommandAckCode.Completed`（HCACK=0）。

`S2F41Handler` 在 `ExecuteRemoteCommandAsync()` 返回后才发送 S2F42，因此 Handler 必须在
Host T3 内完成校验和入队。耗时机台动作应由应用层命令队列执行，最终结果通常
通过设备状态或 Collection Event 上报。

未显式填写完成状态的 Accepted 结果会被标记为 `GemRemoteCommandCompletionStatus.Accepted`，
不会再误判为 Completed。设备业务真正完成或失败后，应显式上报定义中的 CEID：

```csharp
await equipment.ReportRemoteCommandStatusAsync(
    "START",
    GemRemoteCommandCompletionStatus.Completed,
    cancellationToken);
```

## 客户 Profile

多客户项目可使用 JSON Profile 保存 VID、ECID、RPTID、CEID、ALID 和 RCMD
合同，业务执行仍由 C# Handler 完成：

```csharp
GemJsonInterfaceProfile profile =
    await GemInterfaceProfileLoader.LoadJsonAsync(profilePath, cancellationToken);

registry.ApplyProfile(profile);
```

加载器会在临时 Catalog 中校验引用关系并记录原文件 SHA-256。JSON 不支持类型名、
反射或动态代码。参考 [`examples/gem-interface-profile.sample.json`](examples/gem-interface-profile.sample.json)。

## Process Program

`GemProcessProgram` 是 Stream 7 传输载体：`PPID` 标识程序，`PPBODY` 保存 Host 传输的原始内容。
它不等于设备项目的完整 Recipe 领域对象，也不会自动写入 PLC、温控器或 MFC。

```text
Host S7F3
    → Kwy.Communicate.Gem 解析 PPID/PPBODY
    → IGemProcessProgramRepository
    → 项目 Recipe Service 校验并持久化
    → S7F4 返回保存结果

后续 SELECT/START
    → 项目安全互锁和状态机
    → 将已选配方应用到硬件
```

### 默认行为

`GemEquipmentSession` 未传入 Repository 时使用 `UnsupportedGemProcessProgramRepository`。
它不保存任何数据：保存和删除返回 `Rejected`，查询返回空结果。
这样可防止未配置 Recipe Service 时向 Host 误报保存成功。

| 场景 | Repository |
|---|---|
| 不支持 Host 管理配方 | 保留默认 `UnsupportedGemProcessProgramRepository` |
| 单元测试、Simulator | `Kwy.Communicate.Gem.Testing` 中的 `InMemoryGemProcessProgramRepository` |
| 生产设备 | 由设备项目实现 `IGemProcessProgramRepository` |

### 生产项目接入

具体设备项目应实现 `IGemProcessProgramRepository`，把 GEM 语义适配到已有的 Recipe Service：

```csharp
public sealed class EquipmentRecipeRepository : IGemProcessProgramRepository
{
    private readonly IRecipeService recipes; // 具体项目的业务接口

    public EquipmentRecipeRepository(IRecipeService recipes) => this.recipes = recipes;

    public Task<IReadOnlyList<string>> ListPpidsAsync(CancellationToken token = default)
        => recipes.ListIdsAsync(token);

    public async Task<GemProcessProgram?> FindAsync(string ppid, CancellationToken token = default)
    {
        Recipe? recipe = await recipes.FindAsync(ppid, token);
        return recipe is null ? null : ConvertToProcessProgram(recipe);
    }

    public async Task<GemProcessProgramSaveResult> SaveAsync(
        GemProcessProgram program,
        GemProcessProgramSaveOptions? options = null,
        CancellationToken token = default)
    {
        // Parse 应创建项目自有模型，不得长期保留收到的 Secs4Net Item。
        Recipe recipe = Parse(program.Ppid, program.Body);
        RecipeSaveResult result = await recipes.SaveAsync(
            recipe,
            overwrite: options?.Overwrite == true,
            token);

        return result.Status switch
        {
            RecipeSaveStatus.Saved => new(GemProcessProgramSaveStatus.Saved),
            RecipeSaveStatus.AlreadyExists => new(GemProcessProgramSaveStatus.AlreadyExists, result.Message),
            _ => new(GemProcessProgramSaveStatus.Rejected, result.Message)
        };
    }

    public async Task<GemProcessProgramDeleteResult> DeleteAsync(
        string ppid,
        CancellationToken token = default)
    {
        RecipeDeleteResult result = await recipes.DeleteAsync(ppid, token);
        return result.Status switch
        {
            RecipeDeleteStatus.Deleted => new(GemProcessProgramDeleteStatus.Deleted),
            RecipeDeleteStatus.NotFound => new(GemProcessProgramDeleteStatus.NotFound),
            _ => new(GemProcessProgramDeleteStatus.Rejected, result.Message)
        };
    }
}
```

创建会话时显式注入：

```csharp
var processPrograms = new EquipmentRecipeRepository(recipeService);

using IGemEquipmentSession equipment = new GemEquipmentSession(
    client,
    registry,
    processProgramRepository: processPrograms);
```

Repository 的 `Saved` 只表示 PP 已被 Recipe Service 可靠接收；不表示已选择、
已写入硬件或已开始工艺。生产实现应处理版本、重名、审批、容量、权限、
当前设备状态以及事务持久化。

- S7F3/F4 保存、S7F5/F6 读取、S7F17/F18 删除、S7F19/F20 查询目录共用同一 Repository。
- S7F1/F2 通过 `IGemProcessProgramLoadPolicy` 判断 PPID、容量、重复项和业务权限；未配置时默认拒绝。
- S7F3 默认不覆盖同名 PPID；只有项目完成版本、审批和设备状态保护后，才应启用 `Overwrite`。
- `GemProcessProgram` 拥有其 `Body`，使用后应释放；Repository 必须转换或深拷贝需要长期保存的数据。
- S7F17 当前逐项删除；需要原子批量删除时，应由 Recipe Service 提供事务语义。

## Trace 与 Spooling

- Trace 任务的调度生命周期由调用方控制。
- `GemSpoolingService` 提供有界、线程安全、成功后确认删除的顺序补传。
- 默认只缓存明确未连接时产生的 S6F11 和 S5F1；Trace、Terminal 和“发送结果不确定”的异常默认不缓存。
- 队列满时默认拒绝最新消息并向调用方暴露失败，不静默丢弃旧事件；可显式选择 `DropOldest`。
- `InMemoryGemSpoolStore` 不支持断电恢复；生产项目可实现 `IGemSpoolStore` 接入数据库或可靠文件存储。

```csharp
using var spool = new GemSpoolingService(
    new GemSpoolingOptions(
        Enabled: true,
        MaximumMessages: 10_000,
        IncludeAlarms: true,
        IncludeTraceData: false,
        SpoolOnAmbiguousSendFailure: false),
    new InMemoryGemSpoolStore());

using IGemEquipmentSession equipment = new GemEquipmentSession(
    client,
    registry,
    spoolingService: spool,
    processProgramRepository: processPrograms);

// SecsGemClient 自动重连成功后，由应用明确恢复 GEM 会话并补传。
GemSpoolReplayResult replay = await equipment.RecoverCommunicationAsync(
    maximumMessages: 500,
    cancellationToken);
```

`RecoverCommunicationAsync()` 会重新执行 S1F13/S1F14，再按 Sequence 补传，但不会自动恢复
Online Remote。设备必须重新经过本地权限、安全状态和客户约定的 Online 流程。

## 诊断

Diagnostics 输出连接、会话、异常和报文收发信息，不负责日志持久化。

```text
Secs4Net → SecsGemLoggerAdapter（内部）→ IGemDiagnostics → 项目日志系统
```

- `TraceGemDiagnostics`：默认实现，适合开发联调。
- `NullGemDiagnostics.Instance`：关闭所有诊断。
- `GemDiagnosticsOptions`：控制是否记录报文、正文和正文最大长度。
- `GemMessageLogEntry`：包含方向、S/F、W-Bit、System Bytes、名称和可选正文。

生产项目实现 `IGemDiagnostics` 适配已有日志系统。例如 `Microsoft.Extensions.Logging`：

```csharp
public sealed class ApplicationGemDiagnostics : IGemDiagnostics
{
    private readonly ILogger<ApplicationGemDiagnostics> logger;

    public ApplicationGemDiagnostics(ILogger<ApplicationGemDiagnostics> logger)
        => this.logger = logger;

    public void Trace(string message) => logger.LogTrace("{Message}", message);
    public void Information(string message) => logger.LogInformation("{Message}", message);
    public void Warning(string message) => logger.LogWarning("{Message}", message);
    public void Error(string message, Exception? exception = null)
        => logger.LogError(exception, "{Message}", message);
    public void MessageReceived(GemMessageLogEntry message) => LogMessage(message);
    public void MessageSent(GemMessageLogEntry message) => LogMessage(message);

    private void LogMessage(GemMessageLogEntry message)
    {
        logger.LogInformation(
            "GEM {Direction} S{Stream}F{Function} W={W} SB={SystemBytes} Name={Name} Content={Content}",
            message.Direction, message.Stream, message.Function, message.ReplyExpected,
            message.SystemBytes, message.Name, message.Content);
    }
}
```

```csharp
IGemDiagnostics diagnostics = new ApplicationGemDiagnostics(logger);

await using ISecsGemClient client = SecsGemClientFactory.Create(
    config,
    diagnostics,
    new GemDiagnosticsOptions
    {
        LogMessages = true,
        LogMessageContent = false,
        MaxMessageContentLength = 4096
    });
```

日志文件、滚动和保留策略由宿主配置。生产环境应默认关闭
`LogMessageContent`；自定义实现应线程安全、快速且不抛出异常。

## 生产边界

`Kwy.Communicate.Gem` 不提供：

- 全部 SEMI E30 行为、状态子模型与所有 SxFy Handler。
- 具体设备的 VID/ECID/RPTID/CEID/ALID 表。
- PLC、运动控制、工艺流程、权限和安全互锁。
- Recipe、History 和 Spooling 的生产级持久化。
- Host Simulator 图形界面、SEMI 一致性认证或客户专用 Fab 验收脚本。
- E40/E87/E90/E94 业务；相关基础模型位于 `Kwy.Communicate.Gem300`。

`Kwy.Communicate.Gem.Testing` 提供独立的 Host 侧自动化验收基础，可组合 HSMS、S1F13、
Online、S1F3、S2F41、S6F11、S5F1 和 Stream 7 步骤，并输出 JSON/Markdown 报告。
它不被生产 Equipment 运行时引用，不代替客户 Simulator 和认证。

生产项目应通过 Host Simulator 验证通信恢复、Online/Offline 转换、T3 内回复、
Event 数据顺序、Alarm Set/Clear、Remote Command 受理与完成的区分、Process Program 权限与断线补传。

## 许可证

MIT
