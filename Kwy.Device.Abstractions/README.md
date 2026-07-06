# Kwy.Device 架构说明

`Kwy.Device` 是 Kwy 框架中的设备层，用于统一 PLC、仪表、IO 卡、运动控制卡、相机等硬件设备的生命周期、配置、能力接口和业务访问方式。

更完整的架构设计、状态同步、安全联锁、恢复策略和 HSL PLC 接入示例，请阅读 [Kwy.Device 架构说明.md](Kwy.Device%20架构说明.md)。

## 分层结构

```text
Kwy.Device.Abstractions
  只定义设备接口、配置接口、能力接口和注册表抽象。

Kwy.Device.Core
  提供通用基类和默认基础设施，例如 DeviceBase、DeviceRegistry、DeviceFactory。

Kwy.Device.Instruments.*
Kwy.Device.IoCards.*
Kwy.Device.MotionCards.*
Kwy.Device.PLCs.*
Kwy.Device.Cameras.*
  厂商或协议实现层。
```

依赖方向：

```text
业务项目
  -> Kwy.Device.Abstractions
  -> Kwy.Device.Core
  -> 具体设备实现项目
```

`Abstractions` 不应该引用任何厂商 SDK。

## 五类设备

Kwy 当前按设备能力分为五类：

| 类型 | 抽象接口 | 说明 |
| --- | --- | --- |
| Instrument | `IInstrumentDevice` | 仪表，例如 LCR、电源、万用表。 |
| IO | `IIoCardDevice` | IO 卡或可提供 DI/DO 的设备。 |
| Motion | `IMotionCard` | 运动控制卡。 |
| PLC | `IPlcDevice` | PLC 设备。 |
| Vision | `ICameraDevice` | 相机、光源等视觉设备。 |

一个真实设备可以同时实现多个能力接口。

例如固高运动控制卡：

```text
GoogolMotionCardDevice :
  IMotionCard
  IStandardMotionCard
  IAdvancedMotionCard
  IAxisMotionController
  IAxisStatusReader
  IInterpolationMotionController
  IPositionCompareOutput
  IIoCardDevice
```

它既是运动控制卡，也可以作为 IO 设备使用。

## Motion 能力拆分

`IMotionCard` 只表示“这是一个运动控制卡设备”，它保留生命周期和配置能力，不强制所有运动卡都实现 Jog、插补、PSO 等高级能力。

运动能力按 capability interfaces 拆分：

| 接口 | 职责 |
| --- | --- |
| `IMotionCard` | 运动控制卡生命周期与配置入口。 |
| `IStandardMotionCard` | 标准运动卡组合接口：生命周期、配置、单轴运动、轴状态读取、等待能力。 |
| `IAdvancedMotionCard` | 高级运动卡组合接口：`IStandardMotionCard` + 坐标插补能力。 |
| `IAxisMotionController` | 单轴基础运动，例如使能、绝对运动、相对运动、Jog、停止、回零、软限位。 |
| `IAxisStatusReader` | 单轴状态读取，例如规划位置、编码器位置、速度、报警、限位。 |
| `IMotionWaiter` | 等待单轴停止、等待回零完成。 |
| `IInterpolationMotionController` | 坐标系、直线插补、圆弧插补、插补启动/停止、坐标系等待。 |
| `IPositionCompareOutput` | PSO / 位置比较输出能力。 |

这样普通运动卡不需要为了圆弧插补、PSO 这类能力写空实现或抛 `NotSupportedException`。

业务层按能力注入或判断：

```csharp
public sealed class AxisService
{
    private readonly IAxisMotionController axis;
    private readonly IMotionWaiter waiter;

    public AxisService(IAxisMotionController axis, IMotionWaiter waiter)
    {
        this.axis = axis;
        this.waiter = waiter;
    }

    public async Task MoveAsync()
    {
        axis.ServoOn(1);
        axis.MoveAbs(1, 10000, 100);
        await waiter.WaitForAxisStoppedAsync(1);
    }
}
```

可选能力：

```csharp
if (device is IInterpolationMotionController interpolation)
{
    interpolation.MoveArc(...);
}
```

## 生命周期接口

所有设备都实现 `IDevice`：

```csharp
public interface IDevice : IDisposable, IAsyncDisposable
{
    string DeviceId { get; }
    string DeviceName { get; }
    bool IsConnected { get; }
    ConnectionState State { get; }

    event EventHandler<ConnectionStateChangedEventArgs> StateChanged;
    event EventHandler<ErrorOccurredEventArgs> ErrorOccurred;

    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
}
```

设计原则：

```text
ConnectAsync 负责连接硬件或打开底层资源
DisconnectAsync 负责断开硬件或关闭底层资源
DisposeAsync 负责最终释放资源
StateChanged 用于 UI 或日志追踪连接状态
ErrorOccurred 用于统一上报设备错误
```

## 配置接口

设备配置实现 `IDeviceConfig`：

```csharp
public interface IDeviceConfig
{
    bool Validate();
}
```

配置类只表达“如何创建或配置设备”，不直接操作硬件。

示例：

```csharp
var config = new GoogolMotionCardConfig
{
    CardNo = 0,
    AxisCount = 8
};
```

## IDeviceFactory

`IDeviceFactory` 负责“根据配置创建设备”。

它解决的问题是：

```text
我有一个 config，应该创建哪个设备类型？
```

接口：

```csharp
public interface IDeviceFactory
{
    void Register<TConfig, TDevice>(Func<TConfig, TDevice> factory)
        where TConfig : IDeviceConfig
        where TDevice : IDevice;

    IDevice Create(IDeviceConfig config);

    TDevice Create<TDevice>(IDeviceConfig config)
        where TDevice : IDevice;

    IReadOnlyCollection<DeviceRegistration> GetRegistrations();
}
```

示例：

```csharp
factory.Register<GoogolMotionCardConfig, GoogolMotionCardDevice>(
    config => new GoogolMotionCardDevice(config));

var device = factory.Create(new GoogolMotionCardConfig());
```

Factory 不负责保存设备实例，也不负责按名称查找设备。

## IDeviceRegistry

`IDeviceRegistry` 负责“管理已经创建好的设备实例”。

它解决的问题是：

```text
系统里已经有很多设备，我要按 DeviceId 和能力接口找到指定设备。
```

接口：

```csharp
public interface IDeviceRegistry : IAsyncDisposable, IDisposable
{
    IReadOnlyCollection<IDevice> Devices { get; }

    bool TryAdd(IDevice device);
    void AddOrUpdate(IDevice device);
    bool Remove(string deviceId, bool dispose = false);

    bool TryGetDevice(string deviceId, out IDevice device);
    bool TryGetDevice<TCapability>(string deviceId, out TCapability device)
        where TCapability : class;

    IDevice GetRequiredDevice(string deviceId);
    TCapability GetRequiredDevice<TCapability>(string deviceId)
        where TCapability : class;

    IReadOnlyCollection<TCapability> GetDevices<TCapability>()
        where TCapability : class;
}
```

示例：

```csharp
registry.AddOrUpdate(mainMotionCard);
registry.AddOrUpdate(mainIoCard);

var motion = registry.GetRequiredDevice<IMotionCard>("MainMotion");
var io = registry.GetRequiredDevice<IIoCardDevice>("MainIo");
```

Registry 不负责 new 设备。它只保存、查找、枚举和释放设备实例。

泛型查询支持设备类型，也支持能力接口：

```csharp
var motion = registry.GetRequiredDevice<IAxisMotionController>("MainMotion");
var standardMotion = registry.GetRequiredDevice<IStandardMotionCard>("MainMotion");
var advancedMotion = registry.GetRequiredDevice<IAdvancedMotionCard>("MainMotion");
var interpolation = registry.GetRequiredDevice<IInterpolationMotionController>("MainMotion");
var io = registry.GetRequiredDevice<IIoCardDevice>("MainIo");
```

## Factory 与 Registry 的区别

| 组件 | 职责 | 不负责 |
| --- | --- | --- |
| `IDeviceFactory` | 根据配置创建设备 | 保存和查找设备实例 |
| `IDeviceRegistry` | 管理已经创建好的设备实例 | 根据配置决定创建哪个类型 |

推荐流程：

```text
加载配置
  -> IDeviceFactory.Create(config)
  -> IDeviceRegistry.AddOrUpdate(device)
  -> 业务服务按 DeviceId 获取设备
```

## 为什么不只靠接口注入

如果系统里只有一张 IO 卡，直接注入接口可以：

```csharp
public Service(IIoCardDevice ioCard)
{
}
```

但多设备项目会出现歧义：

```text
AdvantechIoCardDevice : IIoCardDevice
GoogolMotionCardDevice : IAdvancedMotionCard, IPositionCompareOutput, IIoCardDevice
```

这时构造函数：

```csharp
public Service(IIoCardDevice ioCard)
```

无法表达你要的是哪一张 IO 卡。

推荐方式：

```csharp
public Service(IDeviceRegistry registry)
{
    mainIo = registry.GetRequiredDevice<IIoCardDevice>("MainIo");
    motion = registry.GetRequiredDevice<IAxisMotionController>("MainMotion");
}
```

也可以在设备数量固定的小项目里直接注入具体类型：

```csharp
public Service(
    AdvantechIoCardDevice ioCard,
    GoogolMotionCardDevice motionCard)
{
}
```

## IOC 注册

Core 注册：

```csharp
services.AddKwyDeviceCore();
```

它会注册：

```text
IDeviceFactory   -> DeviceFactory
IDeviceRegistry  -> DeviceRegistry
```

厂商模块注册示例：

```csharp
services.AddKwyAdvantechIoCard(options =>
{
    options.DeviceDescription = "PCI-1730,BID#0";
});

services.AddKwyGoogolMotionCard(options =>
{
    options.CardNo = 0;
});
```

如果项目里有多个同类型设备，建议不要只依赖单个接口注入，而是把设备加入 `IDeviceRegistry` 后按 `DeviceId` 获取。

## 资源释放

`DeviceRegistry.DisposeAsync()` 会释放注册表中所有设备：

```csharp
await registry.DisposeAsync();
```

业务代码不要手动释放从 IOC 容器注入的单例设备。应用退出时由容器或 Registry 统一释放。

## PLC 心跳

PLC 心跳属于 PLC 协议层能力，抽象为 `IPlcKeepAliveConfig`，默认配置放在 `PlcConfig` 基类中，所有 PLC 实现都可以复用：

```csharp
var config = new HslPlcConfig
{
    IpAddress = "192.168.0.10",
    Brand = HslPlcBrandType.Siemens_S71200,
    KeepAlive = true,
    KeepAliveInterval = 1000,
    KeepAliveAddress = "M0",
    KeepAliveMode = PlcKeepAliveMode.ReadBool
};
```

`KeepAlive` 不等同于 TCP Socket KeepAlive。TCP KeepAlive 只能说明网络通道可能仍存在；PLC KeepAlive 会按 `KeepAliveAddress` 读取一个安全点位，用来确认 PLC 协议读写仍然可用。

`PlcKeepAliveMode` 用于说明心跳地址的数据类型：

| 模式 | 行为 |
| --- | --- |
| `ReadBool` | 读取一个布尔点位。 |
| `ReadInt16` | 读取一个 16 位整数点位。 |
| `ReadInt32` | 读取一个 32 位整数点位。 |
| `ReadFloat` | 读取一个浮点点位。 |
| `ReadBytes` | 读取一个字节/字块。 |

建议使用只读、安全、不影响设备动作的地址作为心跳地址。框架默认不写 PLC 心跳位，避免通用库改变设备逻辑。

## 半导体设备恢复分层

通信层的重连只表示链路恢复，不能直接代表设备可以继续生产。Kwy 将半导体行业常见的恢复职责拆成以下几层：

| 层级 | 抽象 | 说明 |
| --- | --- | --- |
| 协议事务 | `ICommandSession`、`ITransactionManager` | 管理命令、响应、事务 ID、断线后的挂起事务清理。 |
| 设备状态同步 | `IDeviceStateSynchronizer` | 重连后重新读取设备状态，例如 Online、Ready、Alarm、Recipe、Remote。 |
| 安全联锁 | `IDeviceSafetyGuard` | 检查门禁、急停、气压、真空、轴报警、PLC 安全位等条件。 |
| 恢复策略 | `IDeviceRecoveryService`、`RecoveryPolicy` | 决定重连后是人工确认、恢复到 Idle，还是满足安全条件后允许继续。 |
| 设备状态机 | `IEquipmentStateMachine` | 管理 Idle、Running、Recovering、Error、ManualInterventionRequired 等运行状态。 |

默认注册：

```csharp
services.AddKwyDeviceCore();
```

如果要让恢复服务绑定某个具体设备，可注册：

```csharp
services.AddSingleton<MyPlcDevice>();
services.AddKwyDeviceRecoveryFor<MyPlcDevice>();
```

恢复策略示例：

```csharp
var result = await recoveryService.RecoverAsync(
    new DeviceRecoveryContext(
        DeviceId: "MainPlc",
        Policy: RecoveryPolicy.AutoRecoverToIdle),
    cancellationToken);
```

推荐默认策略是 `ManualOnly` 或 `AutoRecoverToIdle`。`AutoResumeWhenSafe` 只适合风险评估明确、状态同步和安全联锁都足够完整的设备。

## 新增设备实现建议

新增设备时建议：

1. 配置类实现 `IDeviceConfig`。
2. 设备类继承对应 Core 基类，例如 `DeviceBase`、`MotionCardBase`、`IoCardBase`、`PlcDeviceBase`。
3. 厂商 SDK 错误码留在厂商模块内处理。
4. 对外只暴露 Kwy 抽象接口。
5. 如果设备同时具备多种能力，可以实现多个能力接口。
6. 在厂商项目中提供 `AddKwyXxxDevice(...)` IOC 注册扩展。
7. 在文档中说明硬件驱动、配置参数、生命周期和资源释放。
