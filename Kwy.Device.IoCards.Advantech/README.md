# Kwy.Device.IoCards.Advantech

`Kwy.Device.IoCards.Advantech` 是基于 Advantech DAQNavi `Automation.BDaq4.dll` 的数字 IO 卡实现。

它面向新 Kwy 设备框架：

```text
Kwy.Device.Abstractions.IO.IIoCardDevice
Kwy.Device.Core.IO.IoCardBase
Kwy.Device.Core.IO.IoBitConverter
Kwy.Device.Core.IO.IoChannelGuard
```

## 支持能力

```text
DI 单点读取
DI 全量读取，返回 bool[64]
DI 64 位快照读取，返回 ulong
DO 单点写入
DO 全量掩码写入
DO 指定点位掩码写入
DO 状态读取，返回 bool[64]
软件脉冲输出
硬件中断事件转发
默认 64 个点位上限
```

## 项目依赖

项目通过本地 DLL 引用 Advantech DAQNavi：

```xml
<Reference Include="Automation.BDaq4">
  <HintPath>DLL\Automation.BDaq4.dll</HintPath>
  <Private>true</Private>
</Reference>
```

目标机器仍然需要正确安装 Advantech 驱动和运行环境。仅复制 `Automation.BDaq4.dll` 通常不足以让板卡正常工作。

## 配置对象

```csharp
var config = new AdvantechIoCardConfig
{
    DeviceDescription = "PCI-1730,BID#0",
    Model = "PCI-1730",
    DiPortCount = 8,
    DoPortCount = 8,
    EnableInterrupt = true,
    InterruptChannel = 0,
    InterruptRisingEdge = true
};
```

参数说明：

| 属性 | 说明 |
| --- | --- |
| `DeviceDescription` | DAQNavi 中识别到的设备描述，例如 `PCI-1730,BID#0`。 |
| `Model` | 对外显示的设备型号。 |
| `DiPortCount` | DI 端口数量，一个端口 8 个点。默认 8，也就是 64 点。 |
| `DoPortCount` | DO 端口数量，一个端口 8 个点。默认 8，也就是 64 点。 |
| `EnableInterrupt` | 是否启用 DAQNavi SnapStart 中断监听。 |
| `InterruptChannel` | 中断源通道。 |
| `InterruptRisingEdge` | 是否使用上升沿触发。`false` 表示下降沿。 |

## 最小使用

```csharp
await using var ioCard = new AdvantechIoCardDevice(config);
await ioCard.ConnectAsync();

bool di0 = ioCard.ReadDiBit(0);
ioCard.WriteDoBit(0, true);

ulong diMask = ioCard.ReadDiPortMask();
bool[] allDi = ioCard.ReadAllDi();
bool[] allDo = ioCard.ReadAllDo();

await ioCard.DisconnectAsync();
```

## 注入到 IOC

业务项目可以直接注册 Advantech IO 卡：

```csharp
services.AddKwyAdvantechIoCard(options =>
{
    options.DeviceDescription = "PCI-1730,BID#0";
    options.Model = "PCI-1730";
    options.DiPortCount = 8;
    options.DoPortCount = 8;
    options.EnableInterrupt = true;
    options.InterruptChannel = 0;
    options.InterruptRisingEdge = true;
});
```

注册后可以注入具体类型：

```csharp
public sealed class StationIoService
{
    private readonly AdvantechIoCardDevice ioCard;

    public StationIoService(AdvantechIoCardDevice ioCard)
    {
        this.ioCard = ioCard;
    }

    public async Task InitializeAsync()
    {
        await ioCard.ConnectAsync();
    }
}
```

也可以注入通用接口：

```csharp
public sealed class StationIoService
{
    private readonly IIoCardDevice ioCard;

    public StationIoService(IIoCardDevice ioCard)
    {
        this.ioCard = ioCard;
    }

    public void SetLight(bool state)
    {
        ioCard.WriteDoBit(0, state);
    }
}
```

生命周期说明：

```text
AdvantechIoCardConfig    Singleton
AdvantechIoCardDevice    Singleton
IIoCardDevice            指向同一个 AdvantechIoCardDevice 实例
```

业务代码不要手动释放从 IOC 注入的 `AdvantechIoCardDevice` / `IIoCardDevice`，由容器在应用退出时释放。业务代码只负责在合适时机调用 `ConnectAsync()` 和 `DisconnectAsync()`。

## 多点 DO 写入

全量写入：

```csharp
// DO0 和 DO2 为 ON，其它可写 DO 为 OFF。
ioCard.WriteDoPortMask((1UL << 0) | (1UL << 2));
```

只修改指定点位：

```csharp
// 只修改 DO4 / DO5。
// DO4 = ON，DO5 = OFF，其它 DO 保持当前状态。
ulong target = 1UL << 4;
ulong changed = (1UL << 4) | (1UL << 5);

ioCard.WriteDoPortMask(target, changed);
```

`AdvantechIoCardDevice` 会先读取当前 DO port 状态，再只写发生变化的 port，避免不必要的端口写入。

## 64 点位规则

Kwy 新框架默认按最多 64 个点位处理 IO：

```text
channel 0  -> port 0 bit 0
channel 7  -> port 0 bit 7
channel 8  -> port 1 bit 0
channel 63 -> port 7 bit 7
```

`ReadAllDi()` 和 `ReadAllDo()` 固定返回长度为 64 的数组。硬件不存在的点位保持 `false`。

`ReadDiPortMask()` 返回 64 位快照，低位对应低通道：

```csharp
ulong mask = ioCard.ReadDiPortMask();

bool di0 = mask.IsPinActive(0);
bool di12 = mask.IsPinActive(12);
```

## 硬件中断

连接时如果 `EnableInterrupt = true`，模块会：

```text
配置 DiintChannels
订阅 InstantDiCtrl.Interrupt
调用 SnapStart()
```

中断触发后会读取当前 DI 快照，并通过 `OnHardwareTriggerReceived` 转发：

```csharp
ioCard.OnHardwareTriggerReceived += (_, mask) =>
{
    bool di0 = mask.IsPinActive(0);
};
```

断开和释放时会：

```text
SnapStop()
取消 Interrupt 事件订阅
```

## 资源释放

业务代码推荐：

```csharp
await using var ioCard = new AdvantechIoCardDevice(config);
```

释放时会：

```text
先断开设备
停止 SnapStart 中断监听
取消 Interrupt 事件订阅
释放 InstantDiCtrl
释放 InstantDoCtrl
释放内部 IO 串行锁
```

不要在释放后继续调用读写方法。

## 错误处理

Advantech SDK 的 `ErrorCode` 属于厂商 DLL 类型，所以错误码处理保留在本模块内，不放入 `Kwy.Device.Core`。

当 SDK 返回非 `Success` 时，模块会：

```text
拼接 DeviceName / DeviceId
触发 ErrorOccurred
抛出 InvalidOperationException
```

示例错误信息：

```text
[PCI-1730/PCI-1730,BID#0] Read DI port mask failed. ErrorCode=...
```

## 注意事项

`DeviceDescription` 必须与 Advantech DAQNavi 中识别到的设备描述一致，例如：

```text
PCI-1730,BID#0
```

如果目标机器没有安装 Advantech 驱动、设备描述不正确，或板卡不支持指定中断通道，`ConnectAsync()` 会抛出异常并进入 `Error` 状态。

真实硬件调试时建议先确认：

```text
DAQNavi 能识别板卡
DeviceDescription 正确
DI / DO port 数量与硬件一致
中断通道确实被硬件支持
```
