# Kwy.Device.PLCs.Hsl

`Kwy.Device.PLCs.Hsl` 基于 HslCommunication 封装 PLC 设备，接入 Kwy 设备层统一生命周期、读写接口、状态同步、安全联锁和恢复服务。

## 设计分层

- `HslPlcDevice`：设备生命周期和 PLC 读写 API，不关心具体 HSL 客户端如何创建。
- `HslPlcClientFactory`：根据 `Transport + Brand` 创建 HSL TCP 或串口客户端。
- `HslPlcConfig`：HSL PLC 强类型配置，保留品牌、站号、Siemens Rack/Slot、HSL 超时等厂商参数。
- `PlcConfig`：PLC 通用连接配置，包括 TCP、串口、KeepAlive。

这样新增 PLC 品牌或协议时，优先扩展 `HslPlcClientFactory`，不要把创建逻辑塞回 `HslPlcDevice`。

## TCP 连接示例

```csharp
services.AddKwyDeviceCore();

services.AddKwyHslPlc(
    deviceId: "PLC.Main",
    deviceName: "主 PLC",
    configure: options =>
    {
        options.Brand = HslPlcBrandType.Siemens_S71200;
        options.Transport = PlcConnectionTransport.Tcp;
        options.IpAddress = "192.168.0.10";
        options.Port = 102;
        options.Rack = 0;
        options.Slot = 1;

        options.KeepAlive = true;
        options.KeepAliveInterval = 1000;
        options.KeepAliveAddress = "M0";
        options.KeepAliveMode = PlcKeepAliveMode.ReadBool;
    });
```

## 串口 RTU 连接示例

```csharp
services.AddKwyHslPlc(
    deviceId: "PLC.Main",
    deviceName: "主 PLC",
    configure: options =>
    {
        options.Brand = HslPlcBrandType.Modbus_Rtu;
        options.Transport = PlcConnectionTransport.Serial;
        options.PortName = "COM6";
        options.BaudRate = 9600;
        options.DataBits = 8;
        options.Parity = ParityType.None;
        options.StopBits = StopBitsType.One;
        options.Station = 238;
    });
```

三菱串口可使用：

```csharp
options.Brand = HslPlcBrandType.Mitsubishi_FxSerial;
options.Transport = PlcConnectionTransport.Serial;
```

## 松下 AFPXHC40T 串口示例

松下 AFPXHC40T 走串口时，通常使用 MEWTOCOL 协议。HSL 对应客户端为 `PanasonicMewtocol`，在 Kwy 中选择：

```csharp
var config = new HslPlcConfig
{
    Brand = HslPlcBrandType.Panasonic_Mewtocol,
    Transport = PlcConnectionTransport.Serial,
    PortName = "COM6",
    BaudRate = 9600,
    DataBits = 8,
    Parity = ParityType.None,
    StopBits = StopBitsType.One,
    Station = 238
};

var plc = new HslPlcDevice("PLC.Main", "松下 AFPXHC40T", config);
await plc.ConnectAsync(cancellationToken);
```

如果通过 DI 注册：

```csharp
services.AddKwyHslPlc(
    deviceId: "PLC.Main",
    deviceName: "松下 AFPXHC40T",
    configure: options =>
    {
        options.Brand = HslPlcBrandType.Panasonic_Mewtocol;
        options.Transport = PlcConnectionTransport.Serial;
        options.PortName = "COM6";
        options.BaudRate = 9600;
        options.DataBits = 8;
        options.Parity = ParityType.None;
        options.StopBits = StopBitsType.One;
        options.Station = 238;
    });
```

`Station` 对应松下 MEWTOCOL 站号。串口参数要和 PLC 通讯口设置保持一致。

## 地址读取

PLC 地址保持 HSL / PLC 原生文本格式，不需要业务层转换进制。

```csharp
bool r130 = await plc.ReadBoolAsync("R130", cancellationToken);
bool r131 = await plc.ReadBoolAsync("R131", cancellationToken);
```

对于三菱 `R` 区，`R130` 表示 PLC 地址文本中的 `R130`，HSL 会按对应 PLC 协议解析。

## KeepAlive

PLC KeepAlive 是协议级健康检查，不是 TCP Socket KeepAlive。它会定时读取一个安全地址，用来确认 PLC 协议读写仍然可用。

```csharp
options.KeepAlive = true;
options.KeepAliveInterval = 1000;
options.KeepAliveAddress = "M0";
options.KeepAliveMode = PlcKeepAliveMode.ReadBool;
```

建议选择只读、安全、不影响设备动作的点位。

## 状态同步与安全联锁

`HslPlcStateSynchronizer` 读取 `StatePoints`，用于同步 Ready、Alarm、Remote、Recipe、AutoMode 等设备状态。

```csharp
DeviceSyncResult result = await synchronizer.SyncStateAsync();
```

`HslPlcSafetyGuard` 读取 `SafetyPoints`，实际值不等于期望值时返回 `DeviceSafetyViolation`。

```csharp
DeviceSafetyResult result = await safetyGuard.CheckAsync();
```

半导体设备建议把急停、安全门、气压、光幕、真空、设备报警等条件纳入安全点位。
