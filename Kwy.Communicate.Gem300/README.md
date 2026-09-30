# Kwy.Communicate.Gem300

`Kwy.Communicate.Gem300` 是 GEM300 对象模型层，基于 `Kwy.Communicate.Gem`。

当前包以 `alpha` 形式发布并采用 MIT 许可证。`alpha` 表示本包目前主要提供对象模型、接口和
内存参考实现，尚未承诺完整的 E87/E90/E40/E94 状态机、SECS 消息映射、持久化恢复或 SEMI
认证；它不是许可证限制，也不影响用户按 MIT 条款使用、修改或分发代码。

当前模块提供：

- E87 风格 Carrier / LoadPort / SlotMap 模型
- E90 风格 Substrate tracking 模型
- E40 ProcessJob 模型
- E94 ControlJob 模型
- Carrier transfer / association / access 状态
- SlotMap verification 状态
- Substrate location 类型
- GEM300 对象历史事件
- 内存管理器实现

设计边界：

```text
Gem300
  负责 300mm 自动化对象、状态和作业关系。

Gem
  负责 GEM E30 的变量、事件、报警、配方和远程命令。

Gem
  基于 Secs4Net 负责 SECS / HSMS 消息通信和 Kwy 统一通信生命周期。
```

真实项目中，Gem300 对象状态变化通常会映射为 GEM Collection Event，再通过 SECS 上报给 Host。
