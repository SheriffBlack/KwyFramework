# Kwy.Communicate 通信层设计

`Kwy.Communicate` 是 Kwy 框架的通信层，统一 TCP、Serial、HTTP、MQTT、OPC UA、GPIB、FluentModbus、SECS/HSMS 等通信协议的生命周期、能力接口、状态事件、KeepAlive 与自动重连。本文档是通信层唯一入口，覆盖当前实现与其他分支中的预留模块规划。

## 1. 职责边界

通信层负责：

- 建立、断开和释放底层连接；维护 `Disconnected`、`Connecting`、`Connected`、`Reconnecting`、`Error` 状态。
- 暴露连接状态变化与通信错误事件。
- 按协议特性执行 KeepAlive、链路级自动重连，以及重连后的协议上下文恢复。
- 提供按能力划分的通信接口与客户端工厂。

通信层不负责：

- 判定安全门、急停、气压、轴限位等安全状态。
- 判定设备是否允许继续生产，或自动恢复整机生产。
- 清除报警，或决定批次、配方、工艺流程是否继续。

以上职责由 `Kwy.Device` 的状态同步、安全联锁和恢复策略，以及 Equipment / Flow 业务层承担。

```text
Host / MES / 仪表 / PLC
        ↓
Kwy.Communicate.*     链路、协议、重连
        ↓
Kwy.Device.*          设备能力、状态同步、设备错误
        ↓
Equipment / Flow      安全联锁、报警、恢复、生产决策
```

## 2. 分层与依赖方向

```text
Kwy.Communicate.Abstractions
  稳定契约：接口、事件、状态、通用配置约定。

Kwy.Communicate.Core
  通用实现：CommunicationClientBase、CommunicationBase、CommunicationFactory。

Kwy.Communicate.*
  协议实现：专属配置、消息模型、第三方通信库与工厂注册。
```

依赖方向只能由下向上：协议模块可引用 `Abstractions`、`Core`；`Abstractions` 不引用具体协议、设备项目或第三方通信库。协议专属配置和第三方类型也不得泄漏到 `Abstractions`。

## 3. 公共能力接口

| 接口 | 职责 | 适用协议 |
| --- | --- | --- |
| `ICommunicationClient` | 连接、断开、状态、错误事件、释放 | 所有通信客户端 |
| `IByteTransport` | 异步字节读写 | TCP、Serial、GPIB |
| `IMessageClient<TMessage>` | 消息事件与异步消息流 | MQTT、OPC UA 订阅 |
| `IRequestClient<TRequest,TResponse>` | 请求—响应 | HTTP |
| `ICommandQueryClient` | 命令、查询与响应 | SCPI / GPIB |
| `ICommunicationFactory` | 按配置创建客户端 | 应用组合根 |
| `IProtocolConfig` | 协议配置与基础验证 | 所有协议配置 |
| `IKeepAliveConfig` | 可选链路健康检查 | TCP、Serial、GPIB |

业务层只依赖实际所需的能力。例如仪表依赖 `IByteTransport`，而不要求 MQTT、HTTP 等协议伪装出不具备的方法。

## 4. 生命周期、重连与 KeepAlive

`CommunicationClientBase` 统一处理 `ConnectAsync`、`DisconnectAsync`、状态变化、释放、单飞重连和重连前旧连接清理。

- 首次 `ConnectAsync` 失败仍将异常返回给调用方；即使开启自动重连，也由后台继续尝试，不能吞掉本次失败。
- 主动 `DisconnectAsync` 不触发自动重连。
- 读写失败时，协议实现调用统一故障入口，不能再自行启动第二个重连循环。
- 重连成功仅代表链路恢复，设备层仍须同步状态并执行安全检查，是否可恢复生产由上层决定。

| 协议 | KeepAlive / 重连策略 |
| --- | --- |
| TCP | Socket/Stream 状态及 I/O 失败进入统一重连 |
| Serial | 串口打开状态与 `ErrorReceived` 进入统一重连 |
| GPIB | 默认检查本地会话；可配置安全的状态查询命令 |
| HTTP | 默认不自动重连；请求方按业务语义决定是否重试 |
| MQTT | 使用协议 KeepAlive；重连后恢复订阅 |
| OPC UA | 使用 Session KeepAlive；重连后恢复订阅 |
| SECS/HSMS | 维护会话与接收循环；设备状态仍由 GEM/设备层表达 |

## 5. 接收模型约束

一个底层接收源只能有一个消费者：

- 主动读取：`IByteTransport.ReadAsync`。
- 消息推送：订阅 `MessageReceived` 或使用 `ReadMessagesAsync`。

不得让后台接收循环、主动读取 API 和外部底层流同时读取同一个 Socket/串口缓冲区；否则会造成响应被抢读、分包错误或随机超时。

## 6. 协议模块

| 模块 | 配置 / 能力 | 状态 |
| --- | --- | --- |
| `Kwy.Communicate.TcpSerial` | `TcpConfig`、`SerialPortConfig`、`HttpConfig`；字节流、HTTP 请求响应 | 当前实现 |
| `Kwy.Communicate.Mqtt` | `MqttConfig`、`IMqttCommunication`、消息流 | 当前实现 |
| `Kwy.Communicate.NI` | `GpibConfig`、字节流、命令查询 | 当前实现 |
| `Kwy.Communicate.OpcUa` | `OpcUaConfig`、节点读写、订阅消息 | 当前实现 |
| `Kwy.Communicate.FMdb` | `MdbConfig`、异步 Modbus 领域操作 | 其他分支 / 扩展模块 |
| `Kwy.Communicate.Secs` | HSMS/SECS-II 配置、消息、`SecsItem`、`ISecsClient` | 预留基础层 |
| `Kwy.Communicate.Secs.Secs4Net` | Secs4Net 适配为 `ISecsClient` | 预留适配层 |
| `Kwy.Communicate.Gem` | SEMI E30 通信/控制状态、报警、报告、变量、配方、远程命令 | 预留行为层 |
| `Kwy.Communicate.Gem300` | Carrier、LoadPort、SlotMap、Substrate、ProcessJob、ControlJob | 预留对象模型层 |
| `Kwy.Communicate.Visa` | VISA 仪器通信 | 预留项目 |

预留模块不承载临时业务代码；正式实现前须补齐配置、能力接口、工厂注册、文档与构建验证。

## 7. 注册与使用

```csharp
var factory = new CommunicationFactory()
    .RegisterTcpSerialClients()
    .RegisterMqtt()
    .RegisterFluentModbus()
    .RegisterGpib();

ICommunicationClient client = factory.CreateClient(config);
await client.ConnectAsync(cancellationToken);
```

异步优先；确有同步兼容需求时，只能通过扩展方法提供，不重新将同步 API 放回核心接口。

旧 V1 API（如 `ICommunicationProtocol`、`CreateProtocol`、`RegisterProtocolCreator`、`SendData`、`ReceiveBatchAsync`）不再恢复。新代码使用 `ICommunicationClient` 与对应能力接口。

## 8. 新增协议规范

1. 新建独立的 `Kwy.Communicate.Xxx` 项目，仅引用 `Abstractions`、`Core` 和该协议的第三方库。
2. 将 `XxxConfig`、专属消息模型、专属接口放入该协议项目。
3. 根据真实模型选择 `IByteTransport`、`IMessageClient<T>`、`IRequestClient<TReq,TRes>` 或专属领域接口。
4. 客户端优先继承 `CommunicationClientBase`；主动字节流客户端可继承 `CommunicationBase`。
5. 失败走统一故障入口；重连后恢复必要的订阅或会话上下文。
6. 提供 `CommunicationFactoryExtensions`，供组合根显式注册。
7. 至少验证配置、连接、断开、重复断开、释放、单飞重连、重连恢复和单一接收消费者。

## 9. 半导体通信分层

```text
Kwy.Communicate.Secs
  HSMS / SECS-II 基础通信与消息模型。

Kwy.Communicate.Secs.Secs4Net
  对第三方 Secs4Net 的适配；第三方类型不向上泄漏。

Kwy.Communicate.Gem
  SEMI E30 行为：状态、报警、事件、报告、变量、配方、远程命令。

Kwy.Communicate.Gem300
  300mm 自动化对象：载具、端口、晶圆、ProcessJob、ControlJob。
```

这些模块提供标准对象与协议能力，不代表已完成客户 EAP/MES 的 SEMI 认证。正式项目仍需按客户 SML、VID/CEID/RPTID、报警表、配方规则与 GEM300 场景做一致性验证。
