# Motion services

Kwy 将运动控制拆分为硬件能力、通用运动服务和业务工艺三层。厂商项目只负责可靠地操作控制卡；点位、联锁和配方不进入厂商 SDK 封装。

## MotionProfile 与工程单位

`MotionProfile` 明确描述速度、加速度和减速度。`AxisEngineeringConfig` 描述某一轴的业务单位和脉冲当量：

```csharp
var axis = new AxisEngineeringConfig
{
    Axis = 1,
    Unit = MotionUnit.Millimeter,
    PulsesPerUnit = 10_000,
    DirectionReversed = false
};

var profile = new MotionProfile(velocity: 100, acceleration: 500, deceleration: 500);
```

配置毫米或角度后，位置使用 `Unit`，速度使用 `Unit/s`，加减速度使用 `Unit/s²`。没有配置的轴使用 `Pulse`，其位置、速度和加减速度均保持厂商 SDK 原生单位，以兼容旧项目。

```csharp
services.AddKwyGoogolMotionCard(config =>
{
    config.Axes.Add(new GoogolAxisConfig
    {
        Axis = 1,
        Name = "X",
        Unit = MotionUnit.Millimeter,
        PulsesPerUnit = 10_000,
        MinimumPosition = 0,
        MaximumPosition = 300,
        MaximumVelocity = 200,
        Home = new GoogolHomeConfig
        {
            Velocity = 20,
            Acceleration = 100,
            Timeout = TimeSpan.FromSeconds(60)
        }
    });
});
```

## 回零状态与等待

`IHomeStatusReader.GetHomeStatus()` 返回 `Idle`、`Running`、`Succeeded` 或 `Failed`：

```csharp
motion.GoHome(1);
HomeStatus result = await waiter.WaitForHomeCompletedAsync(
    axis: 1,
    timeout: TimeSpan.FromSeconds(60),
    cancellationToken);
```

超时抛出 `TimeoutException`，厂商报告回零失败时抛出 `MotionHomeException`。固高实现通过 `GT_HomeSts` 获取状态，超时后停止对应轴。

## 运动安全联锁

```csharp
services.AddKwyMotionServices(options =>
{
    options.MaximumSnapshotAge = TimeSpan.FromMilliseconds(200);
    options.RequireServoEnabled = true;
    options.RequireHomedForPositioning = true;
    options.SoftwareLimits[1] = (-10, 300);

    options.AdditionalRules.Add(request =>
        doorService.IsClosed
            ? null
            : new MotionSafetyViolation("SafetyDoor", "Safety door is open."));
});
```

## 多运动卡

每张运动卡拥有独立的 `IMotionStateMonitor`、`IMotionSafetyGuard` 和 `IAxisMotionExecutor`。同时注册多张卡时，通过 `IMotionRuntimeRegistry` 按稳定的 `DeviceId` 选择运行时：

```csharp
IMotionRuntimeRegistry runtimes = serviceProvider.GetRequiredService<IMotionRuntimeRegistry>();

IAxisMotionExecutor googol = runtimes.GetRequired("Motion.Googol").AxisExecutor;
IAxisMotionExecutor leadshine = runtimes.GetRequired("Motion.Leadshine").AxisExecutor;

IAdvancedMotionCard mainMotion = runtimes.GetRequiredAdvancedMotionCard("Motion.Googol");
IPositionCompareOutput pso = runtimes.GetRequiredCapability<IPositionCompareOutput>("Motion.Googol");
```

只有一张卡时仍可直接注入 `IAxisMotionExecutor`。存在多张卡时再请求无键执行器会抛出明确异常，防止动作被发送到错误控制卡。

业务层建议优先注入 `IMotionRuntimeRegistry`，再按 `DeviceId` 取卡和能力接口。这样同一台设备同时存在固高、雷赛、仿真卡或多张同品牌控制卡时，不会因为无键 `IMotionCard` 注册顺序导致动作发到错误设备。

厂商运动卡注册不再暴露无键 `IMotionCard`、`IAxisMotionController`、`IStandardMotionCard` 等能力接口。业务侧统一通过 `IMotionRuntimeRegistry` 按 `DeviceId` 获取 `IMotionDeviceRuntime`，再访问 `Card`、`StateMonitor` 或 `AxisExecutor`。

`SafeAxisMotionController` 在下发运动前检查连接、快照新鲜度、报警、使能、回零、方向限位、软件限位和业务附加规则。停止、急停和清除报警不会被联锁阻止。

软件联锁不能替代硬件急停、STO、安全继电器和安全 PLC。

## 命名点位

`INamedPositionRepository` 只保存点位，不保存完整产品工艺。Core 提供内存仓储和 JSON 仓储：

```csharp
var repository = new JsonNamedPositionRepository("Config/motion-positions.json");

await repository.SaveAsync(new NamedPositionSet(
    "Load",
    new Dictionary<short, double> { [1] = 120, [2] = 35 }));
```

`INamedPositionMotionService.MoveToAsync()` 会先验证所有轴，再统一下发运动并等待全部轴停止：

```csharp
await namedMotion.MoveToAsync(
    "Load",
    new MotionProfile(100, 500, 500),
    TimeSpan.FromSeconds(10),
    cancellationToken);
```

完整产品配方、运动顺序和工艺条件仍应放在业务项目。

## 仿真运动卡

`Kwy.Device.MotionCards.Simulation` 实现 `IStandardMotionCard`，不需要厂商驱动：

```csharp
services.AddKwySimulationMotionCard(config =>
{
    config.AxisCount = 4;
    config.SimulationSpeedRatio = 5;
});

await card.ConnectAsync();
card.ServoOn(1);
card.MoveAbs(1, 100, new MotionProfile(50, 200, 200));
await card.WaitForAxisStoppedAsync(1, TimeSpan.FromSeconds(5));
```

测试可以通过 `ISimulationMotionControl` 注入位置、报警、正负限位和回零失败，用于验证 UI、等待逻辑和安全联锁。

```text
业务工艺 / 产品配方
        |
NamedPositionMotionService / SafeAxisMotionController
        |
IStandardMotionCard / IMotionStateProvider
        |
GoogolMotionCardDevice 或 SimulationMotionCardDevice
```
## 运动完成与停止

`WaitForAxisStoppedAsync()` 只表达“轴已经停止”，适用于 Jog 停止、人工停止和急停后的状态确认。它不保证轴到达任何目标位置。

点位运动应使用：

```csharp
card.MoveAbs(axis: 1, position: 100, velocity: 50);

MotionCompletionResult result = await card.WaitForAxisCompletedAsync(
    axis: 1,
    targetPosition: 100,
    tolerance: 0.01,
    timeout: TimeSpan.FromSeconds(10));
```

完成等待只有在轴停止且实际位置进入目标容差后才成功。其他结果通过明确异常报告：

| 异常 | 含义 |
| --- | --- |
| `MotionAlarmException` | 运动过程中发生轴报警。 |
| `MotionLimitException` | 到达目标前触发正限位或负限位。 |
| `MotionPositionException` | 轴已经停止，但没有到达目标且没有明确报警或限位状态，常见于人工停止或异常中断。 |
| `TimeoutException` | 在指定时间内没有完成。 |

命名点位服务内部使用完成等待，因此多轴均进入各自目标容差后才会返回成功。插补运动使用 `WaitForCoordinateSystemCompletedAsync()`，同时验证坐标系停止、各轴终点及报警/限位状态。

## AxisMotionExecutor

业务层优先注入 `IAxisMotionExecutor`，不要为每一次点位运动手工组合 `MoveAbs()` 与等待方法：

```csharp
public sealed class TransferService(IAxisMotionExecutor motion)
{
    public Task<MotionCompletionResult> MoveToLoadAsync(CancellationToken cancellationToken)
    {
        return motion.MoveAbsAsync(
            axis: 1,
            position: 120,
            profile: new MotionProfile(100, 500, 500),
            options: new MotionExecutionOptions
            {
                PositionTolerance = 0.01,
                Timeout = TimeSpan.FromSeconds(10)
            },
            cancellationToken);
    }
}
```

`AxisMotionExecutor` 具有以下行为：

- 每个轴只允许一个活动运动，重复启动抛出 `MotionOperationInProgressException`。
- 只订阅共享的 `IMotionStateMonitor`，不会为每次运动创建轴状态轮询循环。
- 到位、报警、正负限位、掉使能、异常停止、超时和取消都有明确结果。
- 取消时默认平滑停止；传感器搜索取消时可配置为急停。
- `NamedPositionMotionService` 已使用该执行器，多轴命名点位会并发启动并统一等待。

执行器订阅无分配参数的 `AxisSnapshotCaptured`，每次硬件采样都会推进活动操作；`AxisSnapshotChanged` 仍只在状态变化时发布给 UI 和报警系统。这样可以识别发生在两个扫描周期之间的快速启动/停止，同时避免每次采样创建 `EventArgs`。

`MotionStateMonitor` 对支持 `IBufferedAxisSnapshotReader` 的控制卡复用轴列表和快照缓冲区。固高实现支持该能力，正常扫描周期不再重复创建轴数组和快照数组。

### 位置门限

位置门限用于非安全关键的提前动作：

```csharp
MotionAxisSnapshot snapshot = await motion.WaitForPositionCrossedAsync(
    axis: 1,
    position: 85,
    direction: PositionCrossingDirection.Positive,
    timeout: TimeSpan.FromSeconds(5),
    cancellationToken);
```

它由共享状态监控器驱动，精度受控制卡状态刷新周期和 Windows 调度影响。精密同步、防撞和高速触发应使用控制器插补、电子凸轮或硬件位置比较功能。

### 传感器搜索

```csharp
SensorSeekResult result = await motion.SeekSensorAsync(
    axis: 2,
    ioDevice: ioCard,
    channel: 5,
    velocity: -10,
    options: new SensorSeekOptions
    {
        ExpectedState = true,
        StopMode = SensorStopMode.ControllerHardwareStop,
        Timeout = TimeSpan.FromSeconds(10)
    },
    cancellationToken);
```

停止模式必须明确选择：

| 模式 | 行为 | 适用场景 |
| --- | --- | --- |
| `ControllerHardwareStop` | Kwy 通过硬件事件或 DI 轮询观察触发并等待轴停止，但不因传感器触发发送软件停止命令。控制器必须事先将该输入绑定为停轴输入。 | 探针、快速输入及可能造成机械损伤的动作。 |
| `SoftwareStop` | Kwy 按 `PollInterval` 读取 DI，触发后调用平滑停止。 | 低速调试和非安全关键传感器。 |

`ControllerHardwareStop` 不是自动配置硬件绑定。控制器参数必须完成快速输入停轴配置。厂商模块发布 `OnHardwareTriggerReceived` 时可更快观察到触发；未发布时 Kwy 仅按 `PollInterval` 补充确认 DI 状态，停车动作仍必须由控制器硬件完成。

## 分配压测

`Kwy.Device.Motion.Benchmarks` 使用同步快照控制器连续执行 50,000 次短运动，测量 `AxisMotionExecutor` 自身的分配和 GC 次数：

```powershell
dotnet run --project Kwy.Device.Motion.Benchmarks -c Release
```

该探针用于决定是否值得进一步引入操作对象池和 `ManualResetValueTaskSourceCore<T>`。在有数据之前，框架保持普通 `TaskCompletionSource<T>`，避免为了理论上的零分配增加复用错误和并发复杂度。

当前 Release 探针结果（50,000 次同步短运动）：

```text
Elapsed: 2,010.6 ms
Allocated: 38,809,512 bytes
Allocated/operation: 776.2 bytes
Gen0: 4
Gen1: 0
Gen2: 0
```

该结果没有出现 Gen 1/2 回收，按真实机械设备的运动调用频率暂不引入池化。后续只有在实际设备长期采样证明 Gen 0 暂停影响节拍时，再考虑池化完成源。
