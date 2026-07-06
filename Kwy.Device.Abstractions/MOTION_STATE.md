# Motion State Design

运动控制的状态读取不应该散落在业务层。Kwy.Device 的设计是：

```text
厂商设备实现真实状态读取
Kwy.Device.Core 负责状态轮询、缓存和变化通知
业务层只读取快照或订阅变化
```

## 架构关系图

```mermaid
flowchart TB
    Hardware["运动控制卡硬件 / 厂商 SDK"]
    Vendor["Kwy.Device.MotionCards.*\n厂商设备实现"]
    Base["Kwy.Device.Core.MotionCardBase\n默认快照转换"]
    Monitor["Kwy.Device.Core.MotionStateMonitor\n后台轮询 / 缓存 / 变化通知"]
    Snapshot["MotionAxisSnapshot\n统一状态快照"]
    Provider["IMotionStateProvider\n读取缓存 / 订阅变化"]
    UI["UI 状态显示"]
    Process["业务流程 / 联锁判断"]
    Alarm["报警 / 日志 / 追溯"]

    Hardware --> Vendor
    Vendor --> Base
    Base --> Snapshot
    Snapshot --> Monitor
    Monitor --> Provider
    Provider --> UI
    Provider --> Process
    Provider --> Alarm
```

## 接口关系图

```mermaid
classDiagram
    class IMotionCard {
        <<interface>>
        +ConnectAsync()
        +DisconnectAsync()
    }

    class IAxisMotionController {
        <<interface>>
        +ServoOn(axis)
        +MoveAbs(axis, position, velocity)
        +MoveJog(axis, velocity)
        +Stop(axis)
    }

    class IAxisStatusReader {
        <<interface>>
        +GetPosition(axis)
        +GetVelocity(axis)
        +GetStatus(axis)
        +IsMoving(axis)
        +IsAlarm(axis)
    }

    class IAxisSnapshotReader {
        <<interface>>
        +GetAxisSnapshot(axis)
    }

    class IMotionWaiter {
        <<interface>>
        +WaitForAxisStoppedAsync(axis)
    }

    class IStandardMotionCard {
        <<interface>>
    }

    class IInterpolationMotionController {
        <<interface>>
        +InitCoordinateSystem(crdIndex, axes)
        +MoveLinear(crdIndex, positions, velocity, acc)
        +MoveArc(crdIndex, x, y, xCenter, yCenter, dir, velocity, acc)
    }

    class IAdvancedMotionCard {
        <<interface>>
    }

    class IMotionStateProvider {
        <<interface>>
        +GetAxisSnapshot(axis)
        +GetAllAxisSnapshots()
        +AxisSnapshotChanged
    }

    class IMotionStateMonitor {
        <<interface>>
        +StartAsync()
        +StopAsync()
        +IsRunning
    }

    IMotionCard <|-- IStandardMotionCard
    IAxisMotionController <|-- IStandardMotionCard
    IAxisStatusReader <|-- IStandardMotionCard
    IAxisSnapshotReader <|-- IStandardMotionCard
    IMotionWaiter <|-- IStandardMotionCard
    IStandardMotionCard <|-- IAdvancedMotionCard
    IInterpolationMotionController <|-- IAdvancedMotionCard
    IMotionStateProvider <|-- IMotionStateMonitor
```

## 状态流转图

```mermaid
sequenceDiagram
    participant Monitor as MotionStateMonitor
    participant Reader as IAxisSnapshotReader
    participant Device as MotionCard Device
    participant Cache as Snapshot Cache
    participant Consumer as UI / Process / Alarm

    Consumer->>Monitor: StartAsync()
    loop PollInterval
        Monitor->>Reader: GetAxisSnapshot(axis)
        Reader->>Device: Read position / velocity / status
        Device-->>Reader: MotionAxisSnapshot
        Reader-->>Monitor: MotionAxisSnapshot
        Monitor->>Cache: Update snapshot
        alt Snapshot changed
            Monitor-->>Consumer: AxisSnapshotChanged
        end
    end
    Consumer->>Monitor: GetAxisSnapshot(axis)
    Monitor-->>Consumer: Cached MotionAxisSnapshot
```

## 核心类型

| 类型 | 职责 |
| --- | --- |
| `MotionAxisSnapshot` | 单轴不可变状态快照，包含位置、编码器位置、速度、原始状态位、运动中、报警、正负限位和时间戳。 |
| `IAxisSnapshotReader` | 从设备读取某一轴的实时快照。 |
| `IMotionStateProvider` | 提供当前缓存快照和 `AxisSnapshotChanged` 变化事件。 |
| `IMotionStateMonitor` | 后台状态监控器，周期读取快照、缓存状态、发布变化通知。 |

## 使用方式

普通业务流程建议读取缓存快照：

```csharp
public sealed class MotionProcess
{
    private readonly IStandardMotionCard motion;
    private readonly IMotionStateProvider stateProvider;

    public MotionProcess(
        IStandardMotionCard motion,
        IMotionStateProvider stateProvider)
    {
        this.motion = motion;
        this.stateProvider = stateProvider;
    }

    public void MoveIfReady()
    {
        var axis1 = stateProvider.GetAxisSnapshot(1);
        if (!axis1.IsAlarm && !axis1.IsMoving)
        {
            motion.MoveAbs(1, 10000, 100);
        }
    }
}
```

UI、报警、日志等场景建议订阅变化事件：

```csharp
public sealed class MotionStatusViewModel
{
    private readonly IMotionStateProvider stateProvider;

    public MotionStatusViewModel(IMotionStateProvider stateProvider)
    {
        this.stateProvider = stateProvider;
        this.stateProvider.AxisSnapshotChanged += OnAxisSnapshotChanged;
    }

    private void OnAxisSnapshotChanged(object? sender, MotionAxisSnapshotChangedEventArgs e)
    {
        var axis = e.Snapshot.Axis;
        var position = e.Snapshot.Position;
        var isAlarm = e.Snapshot.IsAlarm;
    }
}
```

## 注册状态监控器

如果厂商模块没有自动注册状态监控器，可以在业务项目中注册：

```csharp
services.AddKwyMotionStateMonitor(options =>
{
    options.PollInterval = TimeSpan.FromMilliseconds(50);
    options.FirstAxis = 1;
    options.AxisCount = 8;
});
```

`Kwy.Device.MotionCards.Googol` 会默认注册 `IMotionStateMonitor` 和 `IMotionStateProvider`，默认按 `GoogolMotionCardConfig.AxisCount` 从 1 号轴开始采集。

## 分层原则

```text
Kwy.Device.Abstractions
  定义状态模型和状态接口

Kwy.Device.Core
  提供通用状态监控、缓存和事件

Kwy.Device.MotionCards.*
  从厂商 SDK 读取真实状态，并转换为 MotionAxisSnapshot

业务层
  消费状态，不维护状态采集系统
```
