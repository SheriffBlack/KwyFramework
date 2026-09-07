# Kwy.Communicate.Gem300

`Kwy.Communicate.Gem300` 是 GEM300 对象模型层，基于 `Kwy.Communicate.Gem`。

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

Secs
  负责 SECS / HSMS 消息通信。
```

真实项目中，Gem300 对象状态变化通常会映射为 GEM Collection Event，再通过 SECS 上报给 Host。
