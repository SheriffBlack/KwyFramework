# Kwy.Communicate.Secs

`Kwy.Communicate.Secs` 是用于学习 SECS / HSMS / SECS-II 协议的参考实现。

> 该项目不用于生产环境，也不在 `Kwy.Communicate.Gem` / `Gem300` 的生产依赖链上。
> 实际项目请使用 `Kwy.Communicate.Gem`，其底层由 Secs4Net 实现。

当前模块提供：

- HSMS 配置模型 `SecsHsmsConfig`
- SECS 消息模型 `SecsMessage`
- SECS Item 模型 `SecsItem`
- SECS 客户端接口 `ISecsClient`
- 常用消息工厂 `SecsMessageFactory`
- 用于单元测试和上层 GEM 开发的 `InMemorySecsClient`

设计边界：

```text
Secs
  只负责连接、会话、SxFy 消息、Item 数据结构、事务收发。

Gem
  负责 E30 行为：控制状态、报警、事件、变量、配方、远程命令。

Gem300
  负责 E39/E40/E87/E90/E94 对象模型：Carrier、Substrate、ProcessJob、ControlJob。
```

本项目保留简化的消息、Item、会话状态和内存客户端，用于理解协议和编写教学测试。

## 协议类型

- `HsmsMessageType` 对应 HSMS 报头的 `SType`，用于区分 Data、Select、Linktest 等消息。
- `SecsMessageCode` 表示一个完整的 SxFy 组合，并提供常用标准消息常量。
- 自定义消息可以直接使用 `new SecsMessageCode(stream, function)`。

```csharp
var standardMessage = new SecsMessage(
    SecsMessageCode.S1F1AreYouThere,
    ReplyExpected: true);

var equipmentSpecificMessage = new SecsMessage(
    new SecsMessageCode(6, 101));
```
