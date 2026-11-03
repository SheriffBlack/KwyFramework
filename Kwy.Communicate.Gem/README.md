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
- 客户端创建：Factory，不强制使用 DI
- 默认角色：Equipment

## 核心能力

- HSMS/SECS 客户端创建与 `Kwy.Communicate` 生命周期。
- GEM Communication State 和 Control State 基础编排。
- VID、ECID、RPTID、CEID、ALID 与 Remote Command 接口合同。
- JSON/C# Profile、启动校验、目录封闭和 Markdown 接口文档导出。
- Primary Message Router、Parser、Validator 和可组合 Handler。
- Event、Alarm、Equipment Constant、Remote Command、Process Program 和 Trace 基础行为。
- 可替换诊断、Process Program Repository、Spool Store 和安全补传流程。

## 架构设计

### 设计原则

- `Secs4Net` 负责 HSMS 连接、SECS-II 编解码和 Primary/Secondary 事务关联。
- 本库负责 Equipment 侧 GEM 状态、接口合同、消息路由和基础行为编排。
- 设备项目负责工艺、PLC/运动控制、安全互锁、Recipe 持久化和客户接口定义。
- 静态接口定义与运行时数据分离，避免设备运行期间接口编号漂移。
- 核心库不强制 DI：小型程序可直接使用 Factory，宿主项目可自行注册对象。

### 总体分层

```text
设备应用 / Recipe Service / Machine State / PLC
                    ↑ 业务回调与扩展接口
┌───────────────────────────────────────────────────────┐
│ GemEquipmentSession                                  │  GEM 行为编排
│ GemRegistry = Catalog + Data + Commands              │  合同与运行时数据
├───────────────────────────────────────────────────────┤
│ GemPrimaryMessageRouter + Parser/Validator/Handler   │  入站消息处理
│ GemMessageDefinitions + GemMessageFactory            │  出站消息构造
├───────────────────────────────────────────────────────┤
│ ISecsGemClient / SecsGemClient                       │  Kwy 客户端生命周期
├───────────────────────────────────────────────────────┤
│ Secs4Net                                             │  HSMS + SECS-II 事务引擎
└───────────────────────────────────────────────────────┘
                    ↕ TCP/IP
                 Fab Host / EAP
```

依赖方向始终向下。`Kwy.Communicate.Gem` 可以调用设备项目注入的接口或委托，
但不引用具体设备、UI 或业务项目。

生产运行时依赖：

```text
Equipment 项目 ─→ Kwy.Communicate.Gem ─→ Kwy.Communicate.Core ─→ Kwy.Communicate.Abstractions
                                      └─→ Secs4Net
Kwy.Communicate.Gem300 ───────────↗
```

测试与验收依赖：

```text
Equipment.Tests / Host Simulator 项目 ─→ Kwy.Communicate.Gem.Testing ─→ Kwy.Communicate.Gem
```

`Kwy.Communicate.Gem.Testing` 只供测试、冒烟和 Host 侧自动验收项目引用；
生产 Equipment 项目不引用它。核心 GEM 库也不反向依赖 `Gem.Testing` 或 `Gem300`，
且不依赖任何 `Kwy.Device.*` 项目。

### 模块职责

| 模块 | 职责 | 主要入口 |
|---|---|---|
| `Transport` | 配置和管理 Secs4Net、接入 `Kwy.Communicate` 客户端生命周期 | `ISecsGemClient`、`SecsGemClientFactory` |
| `InterfaceCatalog` | 定义并校验 VID、ECID、RPTID、CEID、ALID 和 RCMD | `GemInterfaceCatalogBuilder`、`IGemInterfaceProfile` |
| `Runtime` | 组合静态合同、运行时快照和命令 Handler | `GemRegistry`、`GemDataSnapshot`、`GemCommandRegistry` |
| `Session` | 编排 Communication/Control State 及 Equipment 侧常用行为 | `IGemEquipmentSession`、`GemEquipmentSession` |
| `Protocol/Common` | 定义 S/F 值对象、解析合同和 `Item` 复制等协议基础能力 | `SecsMessageId`、`IGemMessageParser<T>` |
| `Protocol/Messages` | 集中定义 SxFy 语义并构造报文 | `GemMessageDefinitions`、`GemMessageFactory` |
| `Protocol/Streams` | 按 Stream 组织 Parser 和标准 Handler | `S1F3Parser`、`S2F41Handler` 等 |
| `Routing` | 按 S/F 路由 Host Primary Message，保证单次回复和释放 | `GemPrimaryMessageRouter` |
| `RemoteCommands` | 定义、校验和执行 S2F41 命令 | `GemRemoteCommandDefinition`、`GemRemoteCommandValidator` |
| `ProcessPrograms` | 将 Stream 7 PPID/PPBODY 适配到项目 Recipe Service | `IGemProcessProgramRepository` |
| `Reporting` / `Trace` | 事件、报警、运行值和 Trace 采集模型 | `GemVariable`、`GemTraceService` |
| `EquipmentConstants` | 校验 Host 请求的 ECID 变更 | `GemEquipmentConstantChangeValidator` |
| `Spooling` | 有界、按序、成功后删除的断线补传 | `GemSpoolingService`、`IGemSpoolStore` |
| `Diagnostics` | 将 Secs4Net 日志和报文转为宿主可接入的诊断事件 | `IGemDiagnostics` |
| `Validation` | 放置跨能力的通用验证合同和会话策略 | `IGemValidator<T>`、`GemSessionPolicy` |

`Kwy.Communicate.Gem.Testing` 是独立验收与 Simulator 辅助库，不在 Equipment 生产运行时依赖链中。
具体 SxFy Parser 与对应 Handler 同目录放置在 `Protocol/Streams/StreamXX`；
`Protocol/Common` 只保留通用解析合同与 `Item` 读取工具，因此不再保留独立的 `Protocol/Parsing` 目录。
具体能力的 Validator 与所属模块同级放置，例如 `RemoteCommands/GemRemoteCommandValidator`；
`Validation` 只保留跨模块通用类型。

### 核心运行时模型

`GemRegistry` 只是三类对象的组合根，不是数据库或设备业务状态机：

| 对象 | 内容 | 变更时机 |
|---|---|---|
| `Catalog` | 客户接口合同：VID/ECID/RPTID/CEID/ALID/RCMD 定义 | 启动阶段构建，生成后不可变 |
| `Data` | 构造报文所需的当前值和报警快照 | 运行期由设备应用更新 |
| `Commands` | RCMD Handler Key 与编译后 C# Handler 的绑定 | 启动阶段注册，封闭后不可变 |

推荐的启动顺序：

```text
加载 JSON/C# Profile 或使用 Builder
    → Build() 校验引用关系并生成不可变 Catalog
    → 创建 GemRegistry
    → RegisterCommand() 绑定业务 Handler
    → SealConfiguration() 校验 RCMD 定义与 Handler
    → 创建 GemEquipmentSession 和 Router
    → 建立 HSMS / GEM Communication
```

`EstablishCommunicationAsync()` 和 `AcceptCommunicationAsync()` 会自动调用
`SealConfiguration()`；也可在启动自检阶段主动调用，以便更早发现缺失或多余的 Handler。

### 消息处理链路

Host 主动发送 Primary Message 时：

```text
Secs4Net PrimaryMessageWrapper
    → ISecsGemClient.ReadAllAsync()
    → GemPrimaryMessageRouter
    → IGemMessageParser<T>
    → GemSessionPolicy / IGemValidator<T>
    → IGemPrimaryMessageHandler
    → GemEquipmentSession 或设备项目回调
    → Secondary Reply
```

设备应用主动上报时：

```text
设备业务事件
    → GemEquipmentSession
    → Catalog 定义与 Data 快照校验
    → GemMessageFactory
    → ISecsGemClient.SendAsync()
    → W-Bit 为 true 时等待 Host Secondary Reply

断线且报文允许补传
    → GemSpoolingService
    → 恢复 GEM Communication 后按 Sequence 重放
```

Parser 只处理报文结构，Validator 只处理合同和会话条件，Handler 负责流程编排。
业务动作的最终授权仍由设备项目负责。

### 扩展方式

| 需求 | 扩展方式 |
|---|---|
| 增加客户专用 SxFy | 定义 `SecsMessageDefinition`，实现 Parser/Handler 并注册到 Router |
| 增加或变更客户 RCMD | 在 Profile/Catalog 中定义，在设备项目中注册 C# Handler |
| 接入生产 Recipe 存储 | 实现 `IGemProcessProgramRepository` |
| 持久化 Spool | 实现 `IGemSpoolStore` |
| 接入 Serilog/NLog/Microsoft Logging | 实现 `IGemDiagnostics` |
| 执行 ECID 变更、报警启停和 Online 授权 | 向 `GemStandardHandlerSet` 传入设备项目委托 |

不需要为每个客户派生 `GemEquipmentSession`；优先通过 Profile、Repository、Handler 和委托组合。

### 所有权与生命周期

| 对象 | 所有权约定 |
|---|---|
| `ISecsGemClient` | Factory 返回后由调用方 `await using` 释放 |
| `IGemEquipmentSession` | 由调用方 `using` 释放；不会代替调用方释放 Client |
| `GemSpoolingService` | Session 内部创建时由 Session 释放；外部注入时由注入方释放 |
| `GemDataSnapshot` 中的 `Item` | 由调用方保持有效；构造出站报文时会复制 |
| `GemProcessProgram` | 拥有 PPBODY `Item`，调用方必须释放 |
| Repository `FindAsync()` 返回值 | 所有权转移给调用方 |
| `GemMessageFactory` 返回的 `SecsMessage` | 由直接调用 Factory 的一方释放 |

Session 和 Router 已负责释放其内部创建的报文；业务层不应重复释放这些对象。

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

### 2. 建立 GEM 通信并进入 Online

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
