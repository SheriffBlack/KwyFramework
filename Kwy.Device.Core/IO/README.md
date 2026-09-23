# Kwy.Device.Core.IO

`Kwy.Device.Core.IO` 是 Kwy 新设备框架里的通用 IO 基础层，不绑定具体厂商硬件。它负责统一 IO 抽象、64 点位模型、位运算工具、通道校验、逻辑点位映射和硬件中断转发。

## 核心目标

```text
统一 DI / DO 读写 API
默认支持最多 64 个 IO 点位
用 ulong 表示 64 位 IO 快照
隐藏不同板卡的 port / bit / byte 差异
厂商模块只处理自己的 SDK 调用和错误码
```

## 核心类型

| 类型 | 作用 |
| --- | --- |
| `IoCardBase` | IO 板卡基类，提供通用 DO 掩码写入、普通软件定时脉冲和硬件中断事件。 |
| `IIoCardDevice` | 物理 IO 卡能力，仅用于驱动、诊断和基础设施。 |
| `ILogicalIoReader` / `ILogicalIoWriter` | 业务默认使用的逻辑点位读写能力，处理稳定 ID、极性与 Owner。 |
| `IProcessOutputStateController` | 将普通工艺输出收敛到 `ProcessSafeState`；不替代功能安全回路。 |
| `IIoStateMonitor` / `IoStateMonitor` | 逻辑 IO 的初始化、状态监视和诊断服务。 |
| `IoBitConverter` | `byte[]`、`bool[]`、`ulong mask` 之间的通用转换工具。 |
| `IoChannelGuard` | 通道数量、端口数量、通道索引的统一校验工具。 |
| `IoMaskExtensions` | `ulong` IO 快照的扩展方法。 |

## 64 点位模型

Kwy IO 默认按最多 64 个点位处理：

```text
channel 0  -> port 0 bit 0
channel 7  -> port 0 bit 7
channel 8  -> port 1 bit 0
channel 63 -> port 7 bit 7
```

`ulong` 的每一位表示一个通道状态：

```csharp
ulong mask = ioCard.ReadDiPortMask();

bool di0 = mask.IsPinActive(0);
bool di12 = mask.IsPinActive(12);
```

检测状态变化：

```csharp
ulong changed = previousMask ^ currentMask;

if (changed.IsPinActive(5))
{
    bool newState = currentMask.IsPinActive(5);
}
```

## IoBitConverter

`IoBitConverter` 是跨 IO 卡复用的位转换工具。

```csharp
bool[] bits = IoBitConverter.ToBits(portData);
ulong mask = IoBitConverter.ToMask(portData);
byte[] bytes = IoBitConverter.ToPortBytes(mask, portCount: 8);
ulong writableMask = IoBitConverter.CreateWritableMask(channelCount: 64);
```

典型用途：

```text
厂商 SDK 读出 byte[] portData
Core 转成 bool[64] 给 UI 显示
Core 转成 ulong 给高速扫描、边沿检测、中断快照
```

## IoChannelGuard

`IoChannelGuard` 用于统一校验 IO 范围。

```csharp
IoChannelGuard.ValidateChannel(channel, channelCount, nameof(channel));
IoChannelGuard.ValidateChannelCount(channelCount, nameof(channelCount));
IoChannelGuard.ValidatePortCount(portCount, nameof(portCount));
```

默认限制：

```text
MaxChannelCount = 64
MaxPortCount = 8
```

厂商模块不要重复写自己的通道校验，直接复用它即可。

## IoCardBase

厂商 IO 卡一般继承 `IoCardBase`。

子类必须实现：

```csharp
public abstract void WriteDoBit(int channel, bool state);
public abstract bool ReadDiBit(int channel);
public abstract bool[] ReadAllDi();
public abstract bool[] ReadAllDo();
public abstract ulong ReadDiPortMask();
```

基类已经提供：

```csharp
public virtual void WriteDoPortMask(ulong mask);
public virtual void WriteDoPortMask(ulong mask, ulong changedMask);
public virtual void WritePulse(int channel, int durationMs);
```

`WriteDoPortMask(mask)` 表示全量写入：

```csharp
// DO0 和 DO2 为 ON，其它 DO 为 OFF
ioCard.WriteDoPortMask((1UL << 0) | (1UL << 2));
```

`WriteDoPortMask(mask, changedMask)` 表示只修改指定点位：

```csharp
// 只修改 DO4 / DO5，其它 DO 保持当前状态
ulong target = 1UL << 4;
ulong changed = (1UL << 4) | (1UL << 5);

ioCard.WriteDoPortMask(target, changed);
```

如果厂商 SDK 支持端口批量写入，子类应重写 `WriteDoPortMask` 提升性能。否则基类会逐点调用 `WriteDoBit` 作为兜底。

## 逻辑点位与状态监视

`IoStateMonitor` 把业务逻辑名映射到物理 IO 点位。业务服务应分别注入 `ILogicalIoReader`、`ILogicalIoWriter` 或 `IProcessOutputStateController`；`IIoCardDevice` 只应由驱动、诊断或基础设施使用。

`IoPointDefinition` 是唯一的业务点位模型。`IIoPointDefinitionProvider` 是对应的只读定义目录，职责与运动模块的 `IAxisDefinitionProvider` 一致：通过稳定 ID 查询配置定义，而不是让业务、HMI 或卡适配器重复维护字典。运动轴、回零和互锁配置仅保存其 `Id` 引用；运动卡带有板载 IO 时，轴定义和点位定义可引用同一 `DeviceId`。

初始化：

```csharp
IIoPointDefinitionProvider definitions = new IoPointDefinitionProvider(
[
    new IoPointDefinition
    {
        Id = "machine.start",
        Name = "启动按钮",
        DeviceId = ioCard.DeviceId,
        Kind = IoSignalKind.DigitalInput,
        Channel = 0
    },
    new IoPointDefinition
    {
        Id = "tower.green",
        Name = "绿灯",
        DeviceId = ioCard.DeviceId,
        Kind = IoSignalKind.DigitalOutput,
        Channel = 0
    }
]);

IoStateMonitor ioMonitor = serviceProvider.GetRequiredService<IoStateMonitor>();
ioMonitor.Initialize(devices: [ioCard], pointDefinitions: definitions);
```

读取逻辑 DI：

```csharp
bool start = ioMonitor.ReadDi("machine.start");
```

写入逻辑 DO：

```csharp
ioMonitor.WriteDo("tower.green", true);
```

逻辑脉冲：

```csharp
ioMonitor.WriteTimedPulse("camera.trigger", 20);
```

该脉冲由 Windows 定时器实现，只适合普通阀、指示灯等非实时工艺输出；飞拍、比较输出和功能安全必须使用控制器硬件能力。

`IoStateMonitor` 会处理 `IoPointDefinition.Inverted`：

```text
DI 读取：logical = physical ^ Inverted
DO 写入：physical = logical ^ Inverted
```

## 中断与扫描

`IoStateMonitor` 同时支持两种状态更新来源：

```text
硬件中断是可选能力：只有实现 `IHardwareInterruptSource` 的 IO 驱动才发布 `HardwareInterruptReceived`。事件使用 `IoSignalSnapshot`，包含设备 ID、快照时间、来源及可选触发边沿。`IIoCardDevice` 本身只保证同步读写与快照能力；不支持中断的设备继续由 `IIoStateMonitor` 轮询。
后台扫描任务：周期调用 ReadDiPortMask()
```

状态变化事件：

```csharp
ioMonitor.OnIoStateChanged += (pointId, state) =>
{
    Console.WriteLine($"{pointId}: {state}");
};

ioMonitor.OnIoSnapshotReceived += snapshot =>
{
    Console.WriteLine($"{snapshot.DeviceId} {snapshot.Source} {snapshot.Timestamp:O}");
};
```

扫描任务中某张卡发生一次读取异常，不会终止整个 IO 状态监控服务。

## 新增 IO 卡建议

新增厂商 IO 卡时建议：

1. 继承 `IoCardBase`。
2. 配置类实现 `IDeviceConfig`。
3. 默认按 64 点位模型返回 `ReadAllDi()` / `ReadAllDo()`。
4. 使用 `IoBitConverter` 做端口字节与位快照转换。
5. 使用 `IoChannelGuard` 做通道校验。
6. 厂商 SDK 错误码留在厂商模块内处理，不要污染 `Kwy.Device.Core`。
7. 如果支持端口批量写入，重写 `WriteDoPortMask`。
