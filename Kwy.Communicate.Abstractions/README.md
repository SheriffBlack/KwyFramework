# Kwy.Communicate

`Kwy.Communicate` 是 Kwy 框架中容器无关的通信库，为 TCP、串口、HTTP、MQTT、OPC UA、VISA/GPIB、Modbus 和 SECS/GEM 等协议提供统一的生命周期与能力抽象。

本文档是 `Kwy.Communicate.*` 的唯一架构入口。`Kwy.Communicate.Abstractions` 只包含稳定契约，不包含具体协议、第三方 SDK 或依赖注入容器。

## 设计目标

- 统一连接、断开、状态、错误、释放和自动重连行为。
- 按真实交互模型拆分能力接口，不迫使协议实现不具备的功能。
- 已知协议时直接构造具体客户端。
- 仅在运行时根据配置选择协议时使用 Factory。
- 不强制使用 Microsoft DI，不使用 Service Locator。
- 配置只描述创建参数，运行状态保存在客户端中。

## 职责边界

通信层负责：

- 建立、断开和释放底层连接。
- 维护 `Disconnected`、`Connecting`、`Connected`、`Disconnecting`、`Reconnecting` 和 `Error` 状态。
- 发布连接状态变化和通信错误。
- 执行链路级 KeepAlive、自动重连和协议上下文恢复。
- 提供字节、文本命令、消息和请求—响应等通信能力。

通信层不负责安全联锁、设备报警、生产恢复、配方或工艺流程决策。这些职责属于 `Kwy.Device.*`、Equipment 和 Flow 业务层。

```text
Host / MES / 仪器 / PLC
        ↓
Kwy.Communicate.*     链路、协议、会话和重连
        ↓
Kwy.Device.*          设备能力、状态同步和设备错误
        ↓
Equipment / Flow      安全联锁、报警、恢复和生产决策
```

## 项目分层

### Kwy.Communicate.Abstractions

定义 `IProtocolClient`、能力接口、协议配置契约、连接状态、公共事件、Factory 契约和无协议假设的辅助方法。

该项目不得引用具体协议项目、第三方通信 SDK、`IServiceCollection` 或 `IServiceProvider`。

### Kwy.Communicate.Core

- `ProtocolClientBase`：生命周期、状态、KeepAlive 和单飞重连。
- `ByteStreamClientBase`：主动读取字节流的通用基类。
- `ProtocolClientFactoryBuilder`：启动阶段收集 Creator。
- `ProtocolClientFactory`：运行时按配置类型创建客户端。

`Core` 不引用 Microsoft DI，也不保存 `IServiceProvider`。

### Kwy.Communicate.*

具体协议包负责定义协议配置和消息模型、封装第三方 SDK、继承 Core 生命周期基类，并按需提供 Builder 注册扩展。

```text
Abstractions ← Core ← 具体协议包 ← Device / Application
```

## 公共能力接口

| 接口 | 职责 | 典型场景 |
| --- | --- | --- |
| `IProtocolClient` | 连接、尝试连接、断开、状态、错误和释放 | 所有协议客户端 |
| `IByteStreamClient` | 原始字节读写 | TCP、Serial、VISA/GPIB、二进制协议 |
| `ITextCommandClient` | 文本命令与完整查询 | SCPI、GPIB 仪器 |
| `IMessageClient<TMessage>` | 发布、消息事件和异步消息流 | MQTT、订阅模型 |
| `IRequestClient<TRequest,TResponse>` | 强类型请求—响应 | HTTP |
| 协议专属接口 | 明确的领域操作 | Modbus、OPC UA、GEM |

`IByteStreamClient` 是原始字节管道，不知道编码、结束符或消息边界。`ITextCommandClient` 表示完整的文本命令会话，由实现者处理编码、命令终止符和查询响应。同一客户端可以同时实现两种能力。

业务层只依赖真正需要的能力，不得为形式统一而实现协议不支持的方法。

### 方法命名约定

| 能力 | 方法 | 语义 |
| --- | --- | --- |
| `IProtocolClient` | `ConnectAsync` / `TryConnectAsync` / `DisconnectAsync` | 协议客户端生命周期 |
| `IByteStreamClient` | `WriteAsync` / `ReadAsync` | 无消息边界的原始字节流读写 |
| `ITextCommandClient` | `WriteAsync` / `QueryAsync` | 写入文本命令，或执行“写入查询＋读取完整响应” |
| `IMessageClient<TMessage>` | `SendAsync` / `ReadAllAsync` | 发送消息和持续读取消息流 |
| `IRequestClient<TRequest,TResponse>` | `SendAsync` | 执行一次完整的请求—响应 |

方法名按交互语义确定，不强行统一为 `WriteAsync` / `ReadAsync`。例如 `QueryAsync` 同时包含命令写入和完整响应读取，不能命名为单纯的 `ReadAsync`。具体协议仍可以保留更精确的领域方法，例如 MQTT 的 `PublishAsync(topic, payload)`。

## 字节传输辅助方法

`ByteStreamClientExtensions` 只提供无协议假设的通用能力：

- `Write` / `Read`：仅用于调试工具或必须同步调用的兼容场景。
- `WriteTextAsync` / `WriteText`：按指定编码写入文本；工业协议应显式指定编码。
- `ReadExactlyAsync`：合并多次部分读取，直到填满已知长度的缓冲区。

不提供通用 `ReadTextAsync`，因为文本边界可能由换行符、固定长度、长度字段、VISA EOI 或协议状态决定，应由具体协议实现。

## 生命周期与错误语义

`ConnectAsync()` 是标准连接入口。连接失败时，客户端进入 `Error` 状态、发布 `ErrorOccurred`，并将异常返回给调用方。

`TryConnectAsync()` 用于允许硬件离线的宿主启动。连接成功返回 `true`；可预期的连接失败返回 `false`，并保留 `Error` 状态和错误事件；用户取消仍抛出 `OperationCanceledException`。

读写失败不应被静默吞掉。协议实现应更新状态、发布错误、触发统一重连，并将当前 I/O 异常返回给调用方。

## KeepAlive 与自动重连

`ProtocolClientBase` 统一管理连接互斥、状态事件、单飞重连、指数退避、旧连接清理和重连后上下文恢复。

| 协议 | 推荐策略 |
| --- | --- |
| TCP | 根据 Socket/Stream 状态及 I/O 失败进入统一重连 |
| Serial | 根据串口打开状态和错误事件进入统一重连 |
| VISA/GPIB | 默认检查本地会话；仅在配置后发送安全的健康查询命令 |
| HTTP | 默认不自动重连；请求重试由业务语义决定 |
| MQTT | 使用协议 KeepAlive，重连后恢复订阅 |
| OPC UA | 使用 Session KeepAlive，重连后恢复节点订阅 |
| SECS/GEM | 恢复 HSMS/GEM 会话与接收循环 |

协议实现不得另外启动第二套通用重连循环。重连成功只代表链路恢复，不代表设备可以自动恢复生产。

## 接收模型

同一底层数据源必须只有一种消费方式：

- 主动读取：`IByteStreamClient.ReadAsync()`。
- 消息推送：`MessageReceived` 或 `ReadAllAsync()`。

不得让后台接收循环、主动读取 API 和外部底层流同时读取同一个 Socket、串口或会话。

## 创建与 Factory

已知协议时直接创建具体客户端：

```csharp
var config = new TcpConfig { Host = "127.0.0.1", Port = 502 };

await using var client = new TcpCommunication(config);
await client.ConnectAsync(cancellationToken);
await client.WriteAsync(payload, cancellationToken);
```

仅在设备目录、JSON/数据库配置或插件化场景中，才按需组装 Factory：

```csharp
var builder = new ProtocolClientFactoryBuilder();
builder.RegisterTcp();
builder.RegisterSerialPort();
builder.RegisterVisa();

IProtocolClientFactory factory = builder.Build();

await using IProtocolClient client = factory.CreateClient(config);
await client.ConnectAsync(cancellationToken);
```

Builder 只保存“配置类型 → Creator”映射，不是 DI 容器，也不在注册时建立硬件连接。`Build()` 后的 Factory 不可变，客户端由创建它的调用方释放。只注册当前宿主实际需要的协议，不推荐 `RegisterAllCommunications()`。

## DI 边界

可以由 DI 管理无状态 Creator、证书、凭据、诊断、日志、SDK Factory 和只读 `IProtocolClientFactory`。

不应由根容器作为 transient disposable 服务管理 TCP、Serial、MQTT、OPC UA、VISA 和 Modbus 客户端。禁止在 Factory 中保存 `IServiceProvider`、在注册阶段调用 `BuildServiceProvider()`，或让根容器长期捕获临时连接。

## 协议模块

| 模块 | 职责 |
| --- | --- |
| `Kwy.Communicate.TcpSerial` | TCP、Serial 字节流和 HTTP 请求—响应 |
| `Kwy.Communicate.FMdb` | FluentModbus TCP/RTU 适配 |
| `Kwy.Communicate.Mqtt` | MQTT 发布、订阅、消息流和订阅恢复 |
| `Kwy.Communicate.Visa` | VISA/GPIB 资源发现、字节传输和文本命令会话 |
| `Kwy.Communicate.OpcUa` | OPC UA Session、节点读写和订阅 |
| `Kwy.Communicate.Grpc.Client` | TCP/Named Pipe gRPC Channel 生命周期 |
| `Kwy.Communicate.Grpc.Service` | ASP.NET Core gRPC 注册和端点映射 |
| `Kwy.Communicate.Secs` | HSMS/SECS-II 学习与基础模型，不进入生产 GEM 依赖链 |
| `Kwy.Communicate.Gem` | 基于 Secs4Net 的 GEM/E30 行为层 |
| `Kwy.Communicate.Gem300` | Carrier、LoadPort、Substrate、ProcessJob 和 ControlJob 对象模型 |

## 半导体设备约束

通信层自动重连只能恢复链路本身：

1. `AutoReconnect = true` 只表示允许后台尝试恢复链路。
2. 重连成功后设备层必须重新同步设备状态。
3. Equipment 层必须重新执行安全检查。
4. 是否回到 `Idle`、`Ready` 或允许 `Resume` 由上层恢复策略决定。

KeepAlive 只表示链路健康，不等同于设备工艺心跳。PLC 业务地址心跳、GEM 设备状态、Alarm、Event 和 Recovery 必须在设备层或 Equipment 层表达。

`Gem` 和 `Gem300` 提供标准对象与协议能力，不代表已完成 SEMI 认证。正式项目仍需根据客户 EAP/MES 的 SML、VID/CEID/RPTID、Alarm、Recipe 和 GEM300 场景做一致性测试。

## 新增协议规范

1. 创建独立的 `Kwy.Communicate.Xxx` 项目。
2. 协议配置、消息和领域接口放在具体协议项目中。
3. 根据真实模型选择 `IByteStreamClient`、`ITextCommandClient`、`IMessageClient<T>`、`IRequestClient<TReq,TRes>` 或协议专属接口。
4. 通用客户端优先继承 `ProtocolClientBase`；主动字节流客户端可继承 `ByteStreamClientBase`。
5. 第三方 SDK 依赖通过构造函数显式传入。
6. I/O 失败走统一故障入口，重连后恢复必要的订阅或会话上下文。
7. 仅在需要配置驱动创建时提供 Creator 和 Builder 注册扩展。
8. 具体客户端通过私有 `CloneAndValidateConfig()` 复制并验证可变配置；集合、字典等成员必须深复制。
9. 验证配置、连接、断开、重复断开、释放、重连恢复和单一接收消费者。

## 维护原则

1. `Abstractions` 只定义稳定、通用、无第三方依赖的契约。
2. `Core` 统一生命周期，并为配置驱动场景提供可选 Factory。
3. 已知协议直接构造客户端；只有运行时选择协议才使用 Builder/Factory。
4. Creator 通过构造函数接收依赖，不在内部解析 `IServiceProvider`。
5. 客户端由创建它的调用方或 Device 负责释放。
6. 配置只描述创建参数，运行状态保存在客户端或 Manager 中。
7. 通信配置为可变输入模型；客户端构造时必须持有经过验证的防御性副本，不得持有外部可变引用。
8. `Snapshot` 专用于表达某一时刻的不可变运行状态；配置防御性复制不使用 Snapshot 命名。
9. 通用自动重连交给 `ProtocolClientBase`，协议实现不重复建轮子。
10. 业务层依赖能力接口，不依赖第三方通信类型。
11. 同一底层接收源只能有一个消费者。
12. 不得重新引入可变全局 Factory、Service Locator 或重复兼容 API。
