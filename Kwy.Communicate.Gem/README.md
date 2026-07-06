# Kwy.Communicate.Gem

`Kwy.Communicate.Gem` 是 SEMI E30 GEM 行为层，基于 `Kwy.Communicate.Secs`。

当前模块提供：

- GEM 通信状态和控制状态
- Alarm / Event / Report / Variable / Equipment Constant 模型
- Recipe / PPID 模型
- Remote Command 模型
- CEID / RPTID / VID / ALID / ECID 标准标识模型
- Trace / Spooling / History 基础服务
- Host / Equipment 角色上下文
- `GemRegistry` 注册表
- `GemEquipmentService` 默认服务
- S5F1、S6F11、S10F1 等常用消息工厂

设计边界：

```text
Gem
  负责 E30 行为模型和 SxFy 语义。

Gem300
  负责 Carrier、Substrate、ProcessJob、ControlJob 等 300mm 对象模型。
```
