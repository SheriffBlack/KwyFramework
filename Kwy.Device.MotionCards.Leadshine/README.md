# Kwy.Device.MotionCards.Leadshine

基于雷赛 `LTDMC.dll` 封装的 DMC3800 八轴脉冲运动控制卡实现。

## 支持范围

当前版本只兼容 DMC3800，不提供其他雷赛运动控制卡系列的运行时路由。

- 固定 8 轴，Kwy 对外轴号为 `1..8`，LTDMC 原生轴号为 `0..7`。
- 单轴运动使用 DMC3000 系列脉冲 API：`dmc_set_profile()`、`dmc_pmove()`。
- 位置与编码器读取使用 `dmc_get_position()`、`dmc_get_encoder()`。
- 软限位使用 `dmc_set_softlimit()`。
- 直线与圆弧插补使用 `dmc_line_multicoor()`、`dmc_arc_move_multicoor()`。
- 高速位置比较使用整数脉冲比较点，不使用 DMC5X10 的 unit/FIFO API。
- Kwy 统一提供 64 位 DI/DO 掩码。LTDMC 每个端口返回 32 位，因此逻辑通道 `0..31` 映射到端口 0，`32..63` 映射到端口 1。
- `DiChannelCount`、`DoChannelCount` 最大可配置为 64，但必须与现场 DMC3800 及扩展端口的真实点数和端口编号一致。

`DLL/LTDMC.cs` 是厂商完整 P/Invoke 声明，因此其中仍包含其他雷赛系列的函数声明；DMC3800 驱动实现不会调用这些函数。

## 配置

```csharp
services.AddKwyDeviceCore();
services.AddKwyMotionServices();

services.AddKwyLeadshineMotionCard(options =>
{
    options.DeviceId = "Motion.Leadshine";
    options.CardNo = 0;
    options.ConfigFilePath = "dmc.cfg";
    options.ResetOnConnect = true;
    options.LoadConfigOnConnect = true;
    options.DiChannelCount = 16;
    options.DoChannelCount = 16;
    options.DigitalIoActiveLow = true;

    options.Axes.Add(new LeadshineAxisConfig
    {
        Axis = 1,
        Name = "X",
        Unit = MotionUnit.Millimeter,
        PulsesPerUnit = 10_000,
        MinimumPosition = 0,
        MaximumPosition = 300,
        MaximumVelocity = 200,
        MaximumAcceleration = 1_000,
        MaximumDeceleration = 1_000,
        Home = new LeadshineHomeConfig
        {
            Velocity = -20,
            Acceleration = 100,
            Position = 0,
            Offset = 0,
            HomeMode = 1,
            Timeout = TimeSpan.FromSeconds(60)
        }
    });
});
```

`Model` 固定为 `DMC3800`，`AxisCount` 固定为 `8`，不需要也不能通过业务配置修改。

`ConfigFilePath` 指向雷赛厂商配置文件，由 `dmc_download_configfile()` 直接加载。轴工程单位、应用软行程、业务速度上限和回零策略由 Kwy 配置模型管理。

### 64 位 IO 映射

雷赛端口 API 的单次返回值是 `uint`，每个端口只能表达 32 位。驱动通过两个端口组合成 Kwy 的 `ulong`：

```text
逻辑通道 0..31   -> LTDMC port 0, bit 0..31
逻辑通道 32..63  -> LTDMC port 1, bit 0..31
```

例如逻辑通道 32 使用的是 `port 1 / bit 0`，不会执行 `1u << 32`，因此不会回绕并误操作通道 0。单点写、批量写、DI 快照和 DO 状态读取都使用同一映射。

如果现场只存在一个 16 位或 32 位物理端口，应分别将通道数配置为 16 或 32。64 位是框架容量，不代表控制卡必然具有 64 个实际点位。

## 点位运动

生产业务优先注入 `IAxisMotionExecutor`：

```csharp
public sealed class TransferService(IAxisMotionExecutor motion)
{
    public Task<MotionCompletionResult> MoveToAsync(CancellationToken cancellationToken)
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

`IAxisMotionExecutor` 负责每轴单飞、运动完成判断、报警、限位、掉使能、异常停止、超时和取消。连接、使能、Jog、回零、停止和急停使用 `IStandardMotionCard`。

## 插补运动

DMC3800 使用 DMC3000 的普通多轴插补 API，不具备当前 DMC5X10 连续 FIFO 封装的语义。因此每个坐标系在 `StartInterpolation()` 前只允许暂存一个直线段或圆弧段。

```csharp
motionCard.InitCoordinateSystem(1, new short[] { 1, 2 });
motionCard.MoveLinear(1, new double[] { 100, 50 }, velocity: 80, acc: 300);
motionCard.StartInterpolation(1);

await motionCard.WaitForCoordinateSystemCompletedAsync(
    1,
    targetPositions: new double[] { 100, 50 },
    tolerance: 0.01,
    timeout: TimeSpan.FromSeconds(10),
    cancellationToken);
```

同一坐标系在启动前连续添加多个段会抛出明确异常。需要轨迹队列、前瞻或多段连续加工时，应单独增加支持相应硬件能力的控制卡实现，不能把 DMC3800 模拟成 DMC5X10。

## 实机验证

无运动卡环境只能验证编译和接口一致性。上线前至少需要完成：

1. 确认 LTDMC 驱动版本与现场 DMC3800 固件匹配。
2. 低速验证八个轴的方向、脉冲当量、伺服使能、报警和正负限位。
3. 验证回零模式、方向、原点偏移及回零结果码。
4. 验证板载 DI/DO 端口数量和有效电平。
5. 低速验证直线、圆弧插补方向和终点。
6. 使用真实机构验证停止距离、急停和安全联锁。
