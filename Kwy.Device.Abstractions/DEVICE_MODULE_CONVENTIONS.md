# Kwy.Device 设备模块约定

本文定义新增设备领域模块必须遵守的公共结构。目标是让使用者学会一个领域后，能快速定位并使用 IO 卡、运动卡、PLC、相机、仪表，以及后续的机器人、传感器等模块。

这不是要求不同硬件拥有相同能力；统一的是项目边界、依赖方向、组合方式和命名，保留的是领域语义。

## 1. 固定项目骨架

```text
Kwy.Device.Abstractions
    设备通用契约：IDevice、设备标识、连接状态和通用诊断。

Kwy.Device.Core
    设备通用运行时：设备注册、连接生命周期和基础协调。
    只允许跨领域能力进入；禁止放入 IO、运动、PLC、相机、仪表等领域语义。

Kwy.Device.{Domain}.Abstractions
    领域公开模型、接口、能力声明和领域结果。

Kwy.Device.{Domain}.Core
    领域逻辑运行时：注册表、定义校验、逻辑服务、状态管理。

Kwy.Device.{Domain}.{Vendor}
    厂商 SDK 适配、厂商配置、能力映射和设备注册。

Kwy.Device.{Domain}.Simulation / Tests
    仿真设备、契约测试与运行时测试。
```

`Domain` 使用单数业务名，例如 `Camera`、`Instrument`、`IoCard`、`MotionCard`；`PLC` 是行业缩写，始终全大写。

`Kwy.Device.Core` 不是“所有设备逻辑的默认目录”。当代码需要理解轴、点位、寄存器、图像、测量值或厂商能力时，它已经属于某个领域，必须放在对应的 `Kwy.Device.{Domain}.Core`。

## 2. 依赖方向

```text
工艺 / HMI / 设备组合根
        ↓
{Domain}.Abstractions  ←  {Domain}.Core  ←  {Domain}.{Vendor}
        ↑                        ↑
Device.Abstractions        Device.Core
```

必须遵守：

- `*.Abstractions` 不引用 `*.Core`、厂商 SDK、WPF 或具体厂商项目；
- `*.Core` 不引用任何厂商适配项目；
- 厂商项目可以引用本领域的 `Abstractions` 与 `Core`，但不得将 SDK 类型泄漏到公开业务接口；
- 业务流程、配方、HMI 默认只引用领域 `Abstractions` 和业务级 Core 服务，不能直接调用厂商 SDK。

## 3. 服务注册约定

领域 Core 的组合根使用统一命名：

```csharp
services.AddDeviceCore();
services.AddCameraCore();
services.AddIoCardCore();
services.AddMotionCardCore();
services.AddPLCCore();
services.AddInstrumentCore();
```

定义数据使用明确的领域名注册，例如：

```csharp
services.AddIoCardDefinitions(ioPoints);
services.AddAxisDefinitions(axes);
services.AddMotionGroupDefinitions(groups);
services.AddPLCPointDefinitions(plcPoints);
```

厂商项目以 `Add{Vendor}{Domain}` 暴露适配器注册，例如 `AddAdvantechIoCard`、`AddLeadshineMotionCard`、`AddHslPLC`。厂商注册只负责硬件能力和 SDK 适配，不放入工艺规则。

## 4. 业务模型约定

业务层通过稳定逻辑 ID 使用设备资源；物理通道、地址和厂商句柄仅存在于定义与适配层。

| 领域 | 业务资源 | 物理信息 |
| --- | --- | --- |
| IO 卡 | `IoPointDefinition.Id` | `DeviceId`、输入/输出通道、反相与安全状态 |
| 运动卡 | `AxisDefinition.Id`、运动组 ID | `AxisAddress`、控制器通道 |
| PLC | `PlcPointDefinition.Id` | PLC 设备 ID、寄存器/位地址 |
| 相机 | 相机 `DeviceId` | 厂商枚举、序列号、采集句柄 |
| 仪表 | 仪表 `DeviceId` | 通讯参数、协议和厂商命令 |

## 5. 仪表实现集合

`Kwy.Device.Instruments` 是一个有意保留的实现集合包：当前 HIOKI、ADEX 等台式仪表共享相同的字节通信、触发和测量模型，单独拆成大量微包不会降低耦合，反而增加配置与发布负担。

当某厂商需要独立 SDK、独立生命周期、独立发布节奏，或实现规模已经形成稳定边界时，再拆为 `Kwy.Device.Instrument.{Vendor}`。未实现的预留型号不得注册进设备目录或作为可用驱动发布。

## 6. 新领域的落地检查

新增领域前应确认：

1. 公开接口是否只表达该领域的业务语义；
2. 是否存在 `Abstractions → Core → Vendor` 的单向依赖；
3. 是否有唯一、可发现的 Core 注册入口；
4. 业务是否使用逻辑 ID 而不是物理地址或 SDK 句柄；
5. 是否至少提供仿真或 fake 设备的契约测试；
6. 是否明确实时控制、安全硬件与普通 .NET Core 服务的职责边界。

运动控制的实时边界和精密设备专项约束，见 [MOTION_ARCHITECTURE.md](MOTION_ARCHITECTURE.md)。
