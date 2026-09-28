# Kwy.Device 架构说明

`Kwy.Device` 为设备接入提供统一的项目边界、生命周期与组合方式；它不承载工艺流程，也不替代控制器、PLC 或安全回路的实时能力。

## 固定分层

```text
Kwy.Device.Abstractions
    全设备通用契约：IDevice、配置契约、状态和诊断事件。

Kwy.Device.Core
    跨领域运行时：DeviceBase、DeviceRegistry、基础 DI 注册。

Kwy.Device.{Domain}.Abstractions
    某领域公开的模型、接口、能力与结果。

Kwy.Device.{Domain}.Core
    某领域的逻辑运行时、配置校验、状态管理与业务资源解析。

Kwy.Device.{Domain}.{Vendor}
    厂商 SDK 适配、厂商原生配置、能力映射与 DI 注册。

Kwy.Device.{Domain}.Simulation / Tests
    仿真、契约测试和运行时测试。
```

领域名称使用单数：`Camera`、`Instrument`、`IoCard`、`MotionCard`；`PLC` 为行业缩写，始终全大写。

## 依赖方向

```text
业务项目 / HMI / 设备组合根
            ↓
{Domain}.Abstractions  ←  {Domain}.Core  ←  {Domain}.{Vendor}
        ↑                        ↑
Device.Abstractions        Device.Core
```

- `*.Abstractions` 不引用 Core、厂商 SDK 或 UI；
- `*.Core` 不引用厂商适配项目；
- 厂商项目可引用同领域的 Abstractions 与 Core，但不得让 SDK 类型泄漏到业务接口；
- 工艺和 HMI 通过逻辑 ID、领域接口和业务级执行器工作，不能直接依赖物理地址或 SDK 句柄。

## `Kwy.Device.Core` 的边界

`Kwy.Device.Core` 只承载所有设备共同需要、且不包含领域知识的代码：连接生命周期、统一状态事件、资源释放和设备实例注册。

当一段代码需要理解轴、坐标系、IO 点、PLC 地址、图像帧或仪表读数时，必须进入相应的 `Kwy.Device.{Domain}.Core`。这样既避免复制连接状态机，也避免通用 Core 演化成不可维护的大杂烩。

## 资源映射原则

| 领域 | 业务入口 | 物理映射位置 |
| --- | --- | --- |
| IO 卡 | `IoPointDefinition.Id` | IO 点定义与厂商适配 |
| 运动卡 | `AxisDefinition.Id`、运动组 ID | 轴定义与厂商适配 |
| PLC | `PlcPointDefinition.Id` | PLC 点定义与协议适配 |
| 相机 | 相机 `DeviceId` | 相机配置与厂商适配 |
| 仪表 | 仪表 `DeviceId` | 仪表配置、协议和驱动实现 |

运动模块的实时边界、控制器程序与安全策略见 [MOTION_ARCHITECTURE.md](MOTION_ARCHITECTURE.md)；完整模块约定见 [DEVICE_MODULE_CONVENTIONS.md](DEVICE_MODULE_CONVENTIONS.md)。
