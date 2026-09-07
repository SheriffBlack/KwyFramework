# Kwy.Communicate.Secs

`Kwy.Communicate.Secs` 是 SECS / HSMS / SECS-II 基础层。

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

后续真实 HSMS 通信建议通过 Secs4Net adapter 实现 `ISecsClient`，避免上层 GEM/GEM300 直接依赖第三方库。
