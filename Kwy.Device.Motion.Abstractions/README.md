# Kwy.Device.Motion.Abstractions

运动控制模块的公共契约层，定义“运动控制是什么”，不关心“某个厂商 SDK 如何调用”。

业务项目、`Kwy.Device.Motion.Core` 与各厂商适配器共同依赖本项目，从而在不更改业务代码的前提下替换运动卡、运动控制器或 PLC 方案。

## 依赖边界

```text
业务应用
   │  使用轴 ID、运动组、命名位置和执行契约
   ▼
Kwy.Device.Motion.Abstractions
   ▲                         ▲
   │                         │
Kwy.Device.Motion.Core     厂商/设备适配器
                          例如 Leadshine、Googol、ACS、PLC
```

- 可以放置：公共接口、跨厂商数据模型、枚举、业务异常和能力描述。
- 不应放置：厂商 DLL/PInvoke、通信连接细节、轮询线程、缓存实现或 DI 组装代码。
- 不以 EtherCAT、PCIe 或回调机制分割公共契约；它们是适配器的通信或采集能力。

## 核心契约

| 领域 | 主要类型 | 职责 |
| --- | --- | --- |
| 设备与单轴 | `IMotionController`、`IAxisMotionController`、`IMotionProfileController` | 连接设备、下发单轴指令和配置运动参数 |
| 轴定义 | `AxisDefinition`、`IAxisDefinitionProvider` | 将稳定的业务轴 ID 映射到物理设备与通道 |
| 状态采集 | `IAxisSnapshotReader`、`IBulkAxisSnapshotReader`、`IBufferedAxisSnapshotReader`、`IMotionStateSource` | 屏蔽单轴、批量、缓冲、回调和 PDO 等采集差异 |
| 状态消费 | `MotionAxisSnapshot`、`IMotionStateProvider`、`IMotionStateMonitor` | 为完成判定、HMI 和诊断提供统一快照 |
| 业务执行 | `IAxisMotionExecutor`、`IBusinessAxisMotionExecutor`、`IMotionGroupExecutor` | 以业务轴 ID 执行动作，并统一处理完成、超时和故障 |
| 安全与配置 | `IMotionAdmissionGuard`、`IMotionConfigurationValidator`、`IMotionAutoModeGate` | 运动准入、配置一致性检查和自动模式安全门 |
| 高级运动 | 运动组、同步、运动学、轨迹规划和控制器程序契约 | 提供跨厂商的上层表达，但不假设底层一定支持 |

## 轴 ID 与物理地址

`AxisDefinition.Id` 是配方、HMI、运动组和业务流程使用的稳定标识；`DeviceId + Channel` 是厂商适配器使用的物理地址。

```text
AxisDefinition.Id  ──►  DeviceId  ──►  Channel
transport.x             motion-1          1
```

更换卡或调整物理轴号时，只修改定义映射，不修改业务代码。

```csharp
var axis = new AxisDefinition
{
    Id = "transport.x",
    DisplayName = "输送 X 轴",
    DeviceId = "motion-1",
    Channel = 1,
    Engineering = new AxisEngineeringConfig
    {
        Unit = MotionUnit.Millimeter,
        PulsesPerUnit = 1000
    }
};
```

> 业务层优先使用 `IBusinessAxisMotionExecutor` 和轴 ID；只有厂商适配器或 Core 运行时才应直接处理物理通道。

## 能力组合

本项目使用小接口表达可选能力，不要为所有设备制造一个巨大接口。例如：

- 普通脉冲卡可以实现单轴、批量快照和插补能力。
- EtherCAT 控制器可以额外提供 PDO 状态源或控制器程序能力。
- PLC 控轴方案可以通过状态区与命令区实现相同的上层契约。
- 不支持某项能力的设备不实现对应接口，由配置校验和运行时在启动前报告。

## 状态源契约

`IMotionStateSource` 统一了轮询、专用线程、厂商回调、EtherCAT PDO 和混合采集。`MotionStateAcquisitionMode` 用来描述实际采集方式，不代表厂商或产品系列。

状态源必须遵守：

1. 一个状态源只归属一个监视器/设备运行时，不在多个运行时之间共享。
2. `StartAsync` 成功返回前，必须为所有受监视轴发布至少一帧完整初始快照。
3. 原始厂商状态在适配器内转换为 `MotionAxisSnapshot`，不向业务层泄漏状态字。
4. 混合源要先合并高频位置与低频诊断数据，再发布一份完整快照。

## 厂商适配器实现原则

厂商项目通常按“厂商 + 产品/API 家族”划分，例如 `Kwy.Device.Motion.Leadshine.Dmc`、`Kwy.Device.Motion.Leadshine.Ltdmc.Native` 或未来的 ACS 适配器。

适配器应：

- 实现设备真实具备的能力接口。
- 完成工程单位、通道号、厂商错误码和标准快照之间的转换。
- 把 Native DLL、位数、初始化和资源释放留在厂商项目内。
- 复用 Core 的运行时、状态监视、准入检查和执行器，不重复实现通用流程。

