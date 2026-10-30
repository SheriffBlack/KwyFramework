# Kwy.Device.Motion.Core

运动控制模块的通用运行时层，实现 `Kwy.Device.Motion.Abstractions` 中与厂商无关的流程：轴定义映射、状态监视、运动准入、动作执行与完成判定、运动组、命名位置、配置验证以及轨迹规划。

Core 不直接引用厂商 SDK。运动卡、运动控制器和 PLC 适配器只需实现公共契约，再交给 Core 组装。

## 运行时结构

```text
厂商控制器 / PLC 适配器
           │ 指令                       │ 轴快照
           ▼                            ▼
 IAxisMotionController             IMotionStateSource
           │                            │
           │                   MotionStateMonitor
           │                            │ 统一缓存/事件
           └── MotionDeviceRuntime ──┘
                         │
        MotionAdmissionGuard + AxisMotionExecutor
                         │
        BusinessAxisMotionExecutor / MotionGroupExecutor
                         │
                      业务应用
```

`MotionRuntimeFactory` 为一台物理设备组装 `MotionDeviceRuntime`，运行时统一持有：

- `IMotionController`：厂商设备实例。
- `IMotionStateMonitor`：标准轴快照缓存与变化事件。
- `IAxisMotionExecutor`：单轴动作、完成、超时和故障处理。
- `IAxisHomeLifecycle`：回零可信度管理；控制器重连后会使相关轴原点失效。

`IMotionRuntimeRegistry` 以 `DeviceId` 管理多台设备。单设备业务可使用 Core 提供的单例快捷服务；多卡场景应通过轴定义的 `DeviceId` 路由到对应运行时。

## 快速集成

通常由应用层注册公共 Core、轴定义和一个或多个厂商适配器：

```csharp
services.AddMotionCore();

services.AddAxisDefinitions(new[]
{
    new AxisDefinition
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
    }
});

// 再调用对应厂商项目提供的 Add... 扩展注册物理设备。
```

运动组、同步关系、空间运动和离线规划按需注册：

```csharp
services.AddMotionGroupDefinitions(groups);
services.AddMotionSynchronizationDefinitions(virtualAxes, electronicGears, electronicCams);
services.AddSpatialMotion(coordinateFrames, mechanisms);
services.AddOfflineMotionPlanning();
```

## 状态采集

`MotionStateMonitor` 只负责缓存 `MotionAxisSnapshot`、验证初始快照和发布统一事件，不决定设备如何获取状态。

### Core 内置状态源

- `PollingMotionStateSource`：使用 `PeriodicTimer` 周期采集，默认适用于普通 PCI/PCIe 运动卡。
- `DedicatedThreadMotionStateSource`：使用专用后台线程，适用于阻塞式 SDK、较高频轮询或需要隔离线程池抖动的设备。

两者共用 `AxisSnapshotCaptureBuffer`，按以下优先级读取：

1. `IBufferedAxisSnapshotReader`
2. `IBulkAxisSnapshotReader`
3. `IAxisSnapshotReader`

因此，厂商适配器只要提供自身支持的最高效读取能力，无需重复实现轮询、缓冲区和降级逻辑。

```csharp
services.AddMotionStateMonitor(options =>
{
    options.AcquisitionMode = MotionStateAcquisitionMode.Polling;
    options.PollInterval = TimeSpan.FromMilliseconds(50);
    options.Axes = new short[] { 1, 2, 3 };
});
```

对于明确为阻塞式或需要专用线程的 SDK：

```csharp
options.AcquisitionMode = MotionStateAcquisitionMode.DedicatedThread;
options.PollInterval = TimeSpan.FromMilliseconds(5);
```

`DedicatedThread` 不是“越高端越好”的开关。低频监视优先使用 `Polling`；只在 SDK 特性或抖动要求确实需要时才使用专用线程。这些状态源用于监视、诊断和动作完成判定，不替代控制器内部硬实时闭环。

### 厂商状态源

ACS 回调、EtherCAT PDO、PLC 状态区与高端控制器混合采集应由对应适配器继承 `MotionStateSourceBase` 实现，而不是在 Core 中建立一个假的通用 ACS 或 EtherCAT 实现。

厂商状态源只负责：

- 注册与解除厂商回调，或连接 PDO/PLC 过程映像。
- 将原始数据转换为 `MotionAxisSnapshot`。
- 通过 `PublishSnapshot` 发布快照。
- 通过 `PublishError` 发布通信或转换错误。

所有状态源都必须满足首帧安全约束：`StartAsync` 成功返回前，必须已为全部受监视轴发布完整初始快照。仅注册回调后立即返回是不允许的，否则自动模式可能在没有有效状态时启动。

混合源应在适配器内合并快速位置数据与低频诊断数据，然后发布一份完整快照；不要让两个独立状态源相互覆盖新旧数据。

## 动作完成与安全边界

Core 根据标准快照进行动作完成判定，关键参数来自 `AxisDefinition.Defaults` 或本次动作的 `MotionExecutionOptions`：

- `PositionTolerance`：目标位置与反馈位置的允许误差。
- `SettlingTime`：进入容差后必须连续稳定的时间。
- `SettlingVelocityThreshold`：可选的到位速度阈值。
- `Timeout`：动作最长等待时间。

报警、限位、伺服未使能、控制器故障和位置异常会转换为明确的领域异常。`MotionAdmissionGuard` 在下发指令前检查连接、回零可信度、软限位、禁入区与互锁，但它不替代硬件急停、硬限位或安全 PLC。

## 配置与多设备原则

- `DeviceId` 必须在应用内唯一且稳定，用于注册表和轴路由。
- `AxisDefinition.Id` 是业务引用，`DeviceId + Channel` 是物理地址。
- 跨卡运动组只有在底层具备可验证的同步时基和执行能力时才应启用；不把普通上位机顺序调用伪装成硬实时插补。
- 启动自动模式前使用 `IMotionConfigurationValidator` 和 `IMotionAutoModeGate` 检查引用、能力、物理映射与首帧状态。

## 扩展指南

新增厂商或产品系列时：

1. 在厂商项目实现设备真实具备的 Abstractions 能力接口。
2. 普通拉取式 SDK 优先提供批量快照读取，复用 Core 内置状态源。
3. 回调、PDO、PLC 状态区或混合方案继承 `MotionStateSourceBase`。
4. 调用 `MotionRuntimeFactory.Create(...)` 组装运行时，并注册到 `IMotionRuntimeRegistry`。
5. 不在厂商项目重复实现完成判定、准入安全、命名位置或运动组调度。

## 验证

修改公共契约、状态源或执行器后，至少运行：

```powershell
dotnet test .\Kwy.Device.Motion.Tests\Kwy.Device.Motion.Tests.csproj
```

