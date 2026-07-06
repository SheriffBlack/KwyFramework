# Kwy.Communicate

`Kwy.Communicate` 是 Kwy 框架的通信层，负责统一 TCP、Serial、HTTP、MQTT、OPC UA、GPIB、FluentModbus 等通信协议的生命周期、能力接口、状态事件、KeepAlive 和自动重连机制。

主要架构设计、扩展方式和各协议策略请阅读：

- [COMMUNICATION_ARCHITECTURE.md](COMMUNICATION_ARCHITECTURE.md)

核心边界：

```text
Kwy.Communicate.Abstractions
  定义稳定接口、事件、状态和通用配置契约。

Kwy.Communicate.Core
  提供 CommunicationClientBase、CommunicationBase、CommunicationFactory。

Kwy.Communicate.*
  各协议实现模块，保留协议专属配置和第三方依赖。
```

通信层只判断链路是否可用，不判断设备是否安全、是否允许恢复生产。这些能力由 `Kwy.Device` 的状态同步、安全联锁和恢复策略负责。
