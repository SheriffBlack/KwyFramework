# Kwy.Device.MotionCards.Googol

`Kwy.Device.MotionCards.Googol` 是基于固高 GTS SDK `gts.dll` 的运动控制卡实现。

它面向新 Kwy 设备框架：

```text
Kwy.Device.Abstractions.Motion.IMotionCard
Kwy.Device.Abstractions.IO.IIoCardDevice
Kwy.Device.Core.Motion.MotionCardBase
```

也就是说，`GoogolMotionCardDevice` 既可以作为运动控制卡使用，也可以作为 IO 设备接入 `IIoStateMonitor`。

## 支持能力

```text
轴使能 / 断使能
清除轴报警
绝对运动
相对运动
Jog 运动
平滑停止
急停
回零
直线插补
圆弧插补
坐标系启动 / 停止
规划位置读取
编码器位置读取
速度读取
轴状态读取
正负限位 / 报警状态读取
软件限位设置
GPI / GPO 读写
GPO 掩码写入
PSO 飞拍扩展
```

## 项目依赖

项目会把本地 `DLL\gts.dll` 复制到输出目录：

```xml
<None Update="DLL\gts.dll">
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</None>
```

目标机器仍然需要安装固高驱动和运行环境。仅复制 `gts.dll` 不一定足够。

## 配置对象

配置分为两层：

```text
gts.cfg
  固高控制器参数：轴/编码器/脉冲映射、报警与限位输入、有效电平、滤波、PID、停止方式等。

GoogolMotionCardConfig
  Kwy 机器参数：连接、卡号、轴工程单位、机械行程、运动上限、回零以及坐标系定义。
```

`gts.cfg` 由固高工具生成并由 `GT_LoadConfig()` 解析。Kwy 不读取或写入其内部字段。`Axes` 和 `CoordinateSystems` 由 Kwy 使用，不会保存到 `gts.cfg`。

### 参数应该放在哪里

| 参数类别 | 配置位置 | 原因 |
| --- | --- | --- |
| 脉冲、编码器与轴通道映射 | `gts.cfg` | 属于控制器硬件资源，由固高 SDK 解析和应用。 |
| 报警、正负限位、原点输入映射及有效电平 | `gts.cfg` | 属于现场电气接线，必须与控制器配置保持一致。 |
| 滤波、控制环、DAC、停止参数 | `gts.cfg` | 属于控制器底层参数，应使用固高配置工具维护。 |
| 轴名称、工程单位、脉冲当量、方向 | `GoogolAxisConfig` | 属于 Kwy 面向机器和业务代码的坐标语义。 |
| 软件行程、业务速度和加减速度上限 | `GoogolAxisConfig` | 属于应用安全边界，不替代控制器硬件限位。 |
| 回零速度、加速度、偏移和等待超时 | `GoogolHomeConfig` | 属于 Kwy 的回零执行策略；原点输入映射仍在 `gts.cfg`。 |
| 插补轴组合、合成速度、加速度和平滑时间 | `GoogolCoordinateSystemConfig` | 属于应用如何组织多轴插补。 |

连接时的加载顺序如下：

```text
GT_Open(OpenChannel, OpenParameter)
  -> GT_SetCardNo(CardNo)
  -> GT_Reset()                         可选
  -> GT_LoadConfig(ConfigFilePath)      由固高 SDK 加载 gts.cfg
  -> Kwy 应用 Axes 软件限位
  -> 运动时使用 Home 与 CoordinateSystems
```

因此，一台设备通常同时具有两份配置来源：厂商 `gts.cfg` 负责“控制器如何连接硬件”，业务 JSON、IOC 配置或其他配置源中的 `GoogolMotionCardConfig` 负责“Kwy 如何使用这台机器”。二者不是重复配置，也不应自动相互覆盖。

```csharp
var config = new GoogolMotionCardConfig
{
    CardNo = 0,
    OpenChannel = 0,
    OpenParameter = 1,
    Model = "GTS-800",
    ConfigFilePath = "gts.cfg",
    ResetOnConnect = true,
    LoadConfigOnConnect = true,
    AxisCount = 8,
    DiChannelCount = 16,
    DoChannelCount = 16,
    DigitalIoActiveLow = true
};

config.Axes.Add(new GoogolAxisConfig
{
    Axis = 1,
    Name = "X",
    Unit = MotionUnit.Millimeter,
    PulsesPerUnit = 10_000,
    MinimumPosition = -10,
    MaximumPosition = 300,
    MaximumVelocity = 200,
    MaximumAcceleration = 1_000,
    MaximumDeceleration = 1_000,
    Home = new GoogolHomeConfig
    {
        Position = 0,
        Velocity = 20,
        Acceleration = 100,
        Offset = 0,
        Timeout = TimeSpan.FromSeconds(60)
    }
});

config.CoordinateSystems.Add(new GoogolCoordinateSystemConfig
{
    CoordinateSystem = 1,
    Axes = new short[] { 1, 2 },
    MaximumVelocity = 100,
    MaximumAcceleration = 500,
    SmoothingTime = 50
});
```

参数说明：

| 属性 | 说明 |
| --- | --- |
| `OpenChannel` | 传给 `GT_Open()` 的通信通道；GTS 脉冲控制器默认使用 `0`。 |
| `OpenParameter` | 传给 `GT_Open()` 的厂商打开参数，默认使用 `1`。 |
| `CardNo` | 固高卡号。 |
| `Model` | 对外显示的型号。 |
| `ConfigFilePath` | 固高配置文件路径，默认 `gts.cfg`。 |
| `ResetOnConnect` | 连接时是否调用 `GT_Reset()`。 |
| `LoadConfigOnConnect` | 连接时是否调用 `GT_LoadConfig()`。 |
| `AxisCount` | 轴数量，默认 8。 |
| `DiChannelCount` | GPI 通道数量，默认 16。 |
| `DoChannelCount` | GPO 通道数量，默认 16。 |
| `DigitalIoActiveLow` | GTS 常见 IO 为低电平有效，默认 `true`。 |
| `Axes` | 每轴工程单位、脉冲当量、方向、行程上限、运动上限和回零参数。未配置的轴使用原生脉冲单位和默认回零参数。 |
| `CoordinateSystems` | 每个坐标系的轴组合、最大合成速度、最大合成加速度和平滑时间。 |

## IOC 注册

```csharp
services.AddKwyDeviceCore();
services.AddKwyMotionServices();

services.AddKwyGoogolMotionCard(options =>
{
    options.DeviceId = "Motion.Googol";
    options.CardNo = 0;
    options.OpenChannel = 0;
    options.OpenParameter = 1;
    options.Model = "GTS-800";
    options.ConfigFilePath = "gts.cfg";
    options.AxisCount = 8;
    options.DiChannelCount = 16;
    options.DoChannelCount = 16;
    options.DigitalIoActiveLow = true;
    options.Axes.Add(new GoogolAxisConfig
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

`AddKwyMotionServices()` 注册安全检查、运行时注册表、单卡便捷入口和命名点位服务；`AddKwyGoogolMotionCard()` 为该固高卡注册独立的状态监控器与轴执行器。只使用厂商底层接口时可以不调用 `AddKwyMotionServices()`。

固高当前使用 `GT_GetDi` / `GT_GetDo` 的单个 32 位公共 IO 镜像，因此 `DiChannelCount` 和 `DoChannelCount` 最大为 32。Kwy 的 `ulong` 快照是统一容器，不会把不存在的 32..63 通道映射回 0..31。

注册生命周期：

```text
GoogolMotionCardConfig    Singleton
GoogolMotionCardDevice    Singleton
IMotionCard               指向同一个 GoogolMotionCardDevice 实例
IStandardMotionCard       指向同一个 GoogolMotionCardDevice 实例
IAdvancedMotionCard       指向同一个 GoogolMotionCardDevice 实例
IAxisMotionController     指向同一个 GoogolMotionCardDevice 实例
IAxisStatusReader         指向同一个 GoogolMotionCardDevice 实例
IAxisSnapshotReader       指向同一个 GoogolMotionCardDevice 实例
IMotionWaiter             指向同一个 GoogolMotionCardDevice 实例
IInterpolationMotionController 指向同一个 GoogolMotionCardDevice 实例
IPositionCompareOutput    指向同一个 GoogolMotionCardDevice 实例
IIoCardDevice             指向同一个 GoogolMotionCardDevice 实例
IMotionStateMonitor       MotionStateMonitor 单例
IMotionStateProvider      指向同一个 MotionStateMonitor 实例
IAxisMotionExecutor       AxisMotionExecutor 单例，组合命令下发与运动完成判断
```

生产业务中的点位运动优先注入 `IAxisMotionExecutor`：

```csharp
public sealed class MotionService
{
    private readonly IAxisMotionExecutor motion;

    public MotionService(IAxisMotionExecutor motion)
    {
        this.motion = motion;
    }

    public Task<MotionCompletionResult> MoveToWorkAsync(CancellationToken cancellationToken)
    {
        return motion.MoveAbsAsync(
            axis: 1,
            position: 10000,
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

设备连接、使能、Jog、回零、停止、急停以及厂商特有能力仍通过 `IStandardMotionCard`、`IAdvancedMotionCard` 或更细粒度的能力接口调用。连接和使能通常由设备初始化服务统一完成，不应在每一次业务运动中重复执行。

### 接口选择

当前框架没有 `IMotionExecutor` 接口。请区分以下两层：

| 接口 | 层次 | 适用场景 |
| --- | --- | --- |
| `IAxisMotionExecutor` | 业务执行层 | 绝对/相对点位运动、位置门限、传感器搜索；需要单飞、完成判断、超时、取消及异常处理。 |
| `IStandardMotionCard` | 控制卡能力层 | 连接、使能、Jog、回零、停止、急停、状态读取，以及驱动调试。 |
| `IAdvancedMotionCard` | 高级控制卡能力层 | 直线/圆弧插补、坐标系控制等控制器原生多轴能力。 |
| `INamedPositionMotionService` | 业务语义层 | 按名称移动到配方点位，并发启动点位中的多个轴。内部使用 `IAxisMotionExecutor`。 |

不要为了“统一”而增加含义宽泛的 `IMotionExecutor`。单轴点位执行与坐标系插补具有不同的启动、完成和异常语义；后续需要为插补补齐执行层时，更适合增加明确的 `ICoordinateMotionExecutor`。

注入插补能力：

```csharp
public sealed class InterpolationService
{
    private readonly IAdvancedMotionCard motionCard;

    public InterpolationService(IAdvancedMotionCard motionCard)
    {
        this.motionCard = motionCard;
    }

    public async Task MoveArcAsync()
    {
        motionCard.InitCoordinateSystem(1, new short[] { 1, 2 });
        motionCard.MoveArc(1, 10000, 10000, 5000, 0, 0, 100, 1);
        motionCard.StartInterpolation(1);
        await motionCard.WaitForCoordinateSystemStoppedAsync(1);
    }
}
```

注入 IO 接口：

```csharp
public sealed class TriggerService
{
    private readonly IIoCardDevice ioCard;

    public TriggerService(IIoCardDevice ioCard)
    {
        this.ioCard = ioCard;
    }

    public void TriggerCamera()
    {
        ioCard.WritePulse(0, 20);
    }
}
```

业务代码不要手动释放从 IOC 注入的设备实例。由容器在应用退出时释放。业务代码只负责在合适时机调用 `ConnectAsync()` / `DisconnectAsync()`。

## 最小使用（无 IOC / 底层调试）

下面示例用于验证控制卡连接和原始命令，不是生产点位流程的推荐写法：

```csharp
await using var card = new GoogolMotionCardDevice(config);
await card.ConnectAsync();

card.ServoOn(1);
card.MoveAbs(1, position: 10000, velocity: 100);
await card.WaitForAxisCompletedAsync(
    axis: 1,
    targetPosition: 10000,
    tolerance: 0.01,
    timeout: TimeSpan.FromSeconds(10));

double pos = card.GetPosition(1);

await card.DisconnectAsync();
```

## 单轴运动

生产业务推荐让 `IAxisMotionExecutor` 一次完成命令下发和结果等待：

```csharp
MotionCompletionResult result = await motionExecutor.MoveAbsAsync(
    axis: 1,
    position: 100,
    profile: new MotionProfile(50, 200, 200),
    options: new MotionExecutionOptions
    {
        PositionTolerance = 0.01,
        Timeout = TimeSpan.FromSeconds(10)
    },
    cancellationToken);
```

`AxisMotionExecutor` 位于 `Kwy.Device.Core`，通过共享状态监控器提供每轴单飞、到位、限位、报警、掉使能、异常停止、取消和超时语义。

`IStandardMotionCard.MoveAbs()` 与 `IMotionWaiter.WaitForAxisCompletedAsync()` 保留为底层能力，适合驱动调试、厂商适配和框架内部实现。业务层不应反复手工组合二者，否则容易遗漏等待注册、并发互斥、取消停轴或异常状态处理。`WaitForAxisStoppedAsync()` 只用于 Jog、人工停止或急停后的停止确认，不能代表点位运动成功。

绝对运动：

```csharp
card.MoveAbs(axis: 1, position: 10000, velocity: 100, acc: 0.5, dec: 0.5);
```

相对运动：

```csharp
card.MoveRel(axis: 1, distance: 500, velocity: 50);
```

Jog：

```csharp
card.MoveJog(axis: 1, velocity: 20);
card.Stop(axis: 1);
```

急停：

```csharp
card.Abort(axis: 1);
```

回零：

```csharp
card.GoHome(axis: 1);
await card.WaitForHomeCompletedAsync(1);
```

## 插补运动

初始化 XY 坐标系：

```csharp
card.InitCoordinateSystem(crdIndex: 1, axes: new short[] { 1, 2 });
```

压入直线插补：

```csharp
card.MoveLinear(
    crdIndex: 1,
    positions: new double[] { 10000, 5000 },
    velocity: 200,
    acc: 1);
```

启动插补：

```csharp
card.StartInterpolation(1);
await card.WaitForCoordinateSystemStoppedAsync(1);
```

圆弧插补：

```csharp
card.MoveArc(
    crdIndex: 1,
    x: 10000,
    y: 10000,
    xCenter: 5000,
    yCenter: 0,
    dir: 0,
    velocity: 100,
    acc: 1);
```

## IO 用法

GTS 常见 GPI / GPO 为低电平有效，所以默认：

```text
DigitalIoActiveLow = true
```

读取输入：

```csharp
bool di0 = card.ReadDiBit(0);
bool[] allDi = card.ReadAllDi();
ulong diMask = card.ReadDiPortMask();
```

写输出：

```csharp
card.WriteDoBit(0, true);
```

多点写入：

```csharp
// DO0 和 DO2 为 ON，其它 DO 为 OFF
card.WriteDoPortMask((1UL << 0) | (1UL << 2));
```

只修改指定点位：

```csharp
ulong target = 1UL << 4;
ulong changed = (1UL << 4) | (1UL << 5);

card.WriteDoPortMask(target, changed);
```

软件脉冲：

```csharp
card.WritePulse(channel: 0, durationMs: 20);
```

> [!NOTE]
> `WritePulse` 内置防重入与防重叠保护。如果在上一个脉冲尚未结束时再次调用同一通道的 `WritePulse`，系统会自动取消上一次的复位任务并重新计时，以防止数字输出通道被提前置低。

## PSO 飞拍

启用一维位置比较输出：

```csharp
card.EnablePso(
    axis: 1,
    triggerPositions: new[] { 10.0, 20.0, 30.0 },
    pulseScale: 10000.0,
    pulseWidthUs: 20);
```

关闭：

```csharp
card.DisablePso();
```

说明：

```text
triggerPositions 是业务坐标。
pulseScale 用于把业务坐标转换成脉冲。
底层调用 GT_CompareData。
```

## 错误处理

固高 SDK 返回值不为 `0` 时视为失败。

模块会：

```text
拼接 DeviceName / DeviceId
触发 ErrorOccurred
抛出 InvalidOperationException
```

示例：

```text
[GTS-800/0] Start axis 1 motion failed. Result=-1.
```

## 注意事项

真实硬件调试前建议确认：

```text
固高驱动已安装
gts.dll 与硬件/驱动版本匹配
gts.cfg 路径正确，并会复制到运行目录或使用绝对路径
CardNo 正确
轴号是 1-based
GPI / GPO 是否为低电平有效
限位、报警、伺服使能逻辑已经和电气图核对
```

### 💡 架构与安全改进说明

1. **批量状态拉取 (IBulkAxisSnapshotReader)**：
   当前实现已将 `GoogolMotionCardDevice` 升级为 `IBulkAxisSnapshotReader`。当后台监控 `MotionStateMonitor` 运行轮询时，会自动启用批量读取机制。所有轴的硬件 P/Invoke 状态拉取将在单次 Lock 周期内同步、连续快速完成，极大地减少了占锁耗时与上下文切换开销，保证了紧急状态下 `Stop` / `Abort` 动作的高响应实时性。
2. **多向回零支持**：
   回零参数验证与 API 中已解除了对速度绝对值的限制。`GoogolHomeConfig.Velocity` 允许配置为负数（负向搜索原点），运动指令会保留符号直接下发给固高控制器。
3. **回零安全打断**：
   在轴触发 `Stop` 或 `Abort` 时，控制逻辑会强制将该轴从“正在回零 (homingAxes)”的追踪队列中移除，从而杜绝了在人工终止或报警中止时因 SDK 状态清零而被误判为“回零成功”的隐患。

---
*当前实现已完成并发锁与重入优化，已通过编译验证与单元测试接口验证。*
