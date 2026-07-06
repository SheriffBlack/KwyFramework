# Kwy 相机模块设计与使用

Kwy 将相机的生命周期与可选能力分开设计，使海康、大华、Basler 等不同品牌的相机可以共存，同时避免业务层直接依赖厂商 SDK 类型。

## 项目分层

```text
Kwy.Device.Abstractions
  CameraFrame、CameraConfig、ICameraDevice
  IFrameSource、ISoftwareTriggerCamera、ICameraParameterController
  ICameraRegistry

Kwy.Device.Core
  CameraBase：管理采集生命周期、帧事件和异步等待下一帧
  CameraRegistry：按 DeviceId 查找相机及其能力接口

Kwy.Device.Cameras.HikVision
  HikCameraConfig
  HikCameraDevice
  AddKwyHikVisionCamera()
```

依赖方向如下：

```text
业务项目
  -> Kwy.Device.Abstractions
  -> Kwy.Device.Core
  -> Kwy.Device.Cameras.HikVision / Dahua / Basler
```

`Kwy.Device.Abstractions` 和 `Kwy.Device.Core` 不引用任何相机厂商 SDK。

## 能力接口

`ICameraDevice` 只表示相机的身份、连接、配置和释放能力，不强制所有相机实现软触发或参数设置。

| 接口 | 职责 |
| --- | --- |
| `ICameraDevice` | 相机生命周期与配置入口。 |
| `IFrameSource` | 启动、停止采集，接收帧事件，异步等待下一帧。 |
| `ISoftwareTriggerCamera` | 执行软件触发。 |
| `ICameraParameterController` | 动态设置曝光时间和增益。 |
| `ICameraRegistry` | 按稳定的 `DeviceId` 查找多个相机及其能力。 |

业务代码只获取实际需要的能力：

```csharp
ICameraDevice camera = registry.GetRequired("Camera.Top");
IFrameSource frames = registry.GetRequiredCapability<IFrameSource>("Camera.Top");
```

如果相机不支持指定能力，`GetRequiredCapability<T>()` 会抛出明确的 `NotSupportedException`。

## 图像帧所有权

`CameraFrame.PixelData` 是从厂商 SDK 缓冲区复制出来的独立托管内存。

因此：

- `FrameArrived` 回调结束后，图像数据仍然有效。
- 厂商 SDK 释放原始图像缓冲区后，业务层仍可保存或处理该帧。
- 业务层不需要调用海康、大华或 Basler SDK 的释放方法。
- 同一套算法代码可以处理不同品牌相机产生的 `CameraFrame`。

这种方式优先保证所有权清晰和使用安全。只有经过分配率、GC 和帧率压测，确认复制成为性能瓶颈后，才建议引入内存池；届时必须同时设计明确的租约与释放协议。

## CameraFrame 字段

| 字段 | 说明 |
| --- | --- |
| `PixelData` | 独立拥有的图像字节数据。 |
| `Width` / `Height` | 图像宽度和高度。 |
| `FrameNumber` | 厂商 SDK 提供的帧编号。 |
| `PixelFormat` | 像素格式名称，例如 `Mono8`、`BayerRG8`。 |
| `Stride` | 每行字节数；为 `0` 时表示驱动未提供。 |
| `Timestamp` | 框架接收到该帧的 UTC 时间。 |

`PixelData` 保留相机输出的原始像素格式，不会自动转换为 RGB 或 WPF 图像。像素格式转换属于图像处理或 UI 显示层职责。

## 注册海康相机

首先注册设备基础服务和海康相机：

```csharp
services.AddKwyHikVisionCamera(config =>
{
    config.DeviceId = "Camera.Top";
    config.DeviceName = "顶部检测相机";
    config.SerialNumber = "DA1234567";
    config.TransportType = CameraTransportType.GigE;

    config.ExposureTimeUs = 8_000;
    config.Gain = 0;

    config.TriggerModeEnabled = true;
    config.TriggerSource = CameraTriggerSource.Line0;

    config.FrameBufferCount = 4;
    config.FrameReceiveTimeout = TimeSpan.FromMilliseconds(500);
});
```

`AddKwyHikVisionCamera()` 内部会确保 `AddKwyDeviceCore()` 已注册，因此业务项目不需要为了相机重复调用。

## 注册多个相机

每次调用 `AddKwyHikVisionCamera()` 注册一个独立相机实例：

```csharp
services.AddKwyHikVisionCamera(config =>
{
    config.DeviceId = "Camera.Top";
    config.DeviceName = "顶部相机";
    config.SerialNumber = "TOP-SN";
});

services.AddKwyHikVisionCamera(config =>
{
    config.DeviceId = "Camera.Bottom";
    config.DeviceName = "底部相机";
    config.IpAddress = "192.168.1.21";
});
```

所有相机的 `DeviceId` 必须唯一。后续注册大华或 Basler 相机时，也使用同一个 `ICameraRegistry` 查找：

```csharp
ICameraRegistry registry = serviceProvider.GetRequiredService<ICameraRegistry>();

ICameraDevice topCamera = registry.GetRequired("Camera.Top");
ICameraDevice bottomCamera = registry.GetRequired("Camera.Bottom");
```

## 相机查找规则

配置必须至少提供序列号或 IP 地址之一：

```csharp
config.SerialNumber = "DA1234567";
```

或者：

```csharp
config.IpAddress = "192.168.1.21";
```

推荐生产项目优先使用序列号，因为序列号不会随着网络配置变化。IP 地址适合固定规划的 GigE 相机网络。

`TransportType` 可选值：

| 值 | 说明 |
| --- | --- |
| `Auto` | 同时枚举 GigE 和 USB 相机。 |
| `GigE` | 只枚举 GigE 相机。 |
| `Usb` | 只枚举 USB 相机。 |

## 连续采集

```csharp
ICameraRegistry registry = serviceProvider.GetRequiredService<ICameraRegistry>();
ICameraDevice camera = registry.GetRequired("Camera.Top");
IFrameSource frames = registry.GetRequiredCapability<IFrameSource>("Camera.Top");

frames.FrameArrived += (_, frame) =>
{
    Process(
        frame.PixelData.Span,
        frame.Width,
        frame.Height,
        frame.PixelFormat);
};

await camera.ConnectAsync(cancellationToken);
await frames.StartGrabbingAsync(cancellationToken);
```

`StartGrabbingAsync()` 是单飞操作。并发或重复调用不会重复启动 SDK 取流线程。

`FrameArrived` 在相机接收线程上触发。事件处理器中不要直接执行耗时算法或更新 WPF 控件，应将帧交给独立处理队列，或者切换到 UI 调度器。

## 软件触发采图

软件触发前必须先启动取流。等待任务应在发送触发命令之前创建，避免相机响应过快导致漏掉图像帧。

```csharp
ICameraDevice camera = registry.GetRequired("Camera.Top");
IFrameSource frames = registry.GetRequiredCapability<IFrameSource>("Camera.Top");
ISoftwareTriggerCamera trigger =
    registry.GetRequiredCapability<ISoftwareTriggerCamera>("Camera.Top");

await camera.ConnectAsync(cancellationToken);
await frames.StartGrabbingAsync(cancellationToken);

Task<CameraFrame> pendingFrame = frames.WaitForNextFrameAsync(
    TimeSpan.FromSeconds(2),
    cancellationToken);

await trigger.ExecuteSoftwareTriggerAsync(cancellationToken);
using CameraFrame frame = await pendingFrame;
```

调用顺序为：

```text
连接相机
  -> 启动取流
  -> 创建等待下一帧任务
  -> 发送软件触发
  -> 等待并取得 CameraFrame
```

## 硬件触发采图

硬件触发时，相机的触发源配置为 `Line0` 到 `Line3`：

```csharp
config.TriggerModeEnabled = true;
config.TriggerSource = CameraTriggerSource.Line0;
```

PLC、运动控制卡或 IO 卡负责产生实际的硬件电平。相机模块不通过软件模拟硬件触发，只负责持续取流并发布触发后产生的图像帧。

典型流程：

```text
启动相机取流
  -> 设备运动到拍照位置
  -> IO / PSO 输出触发脉冲
  -> 相机曝光并输出图像
  -> FrameArrived 发布 CameraFrame
  -> 视觉算法处理
```

高速飞拍建议优先使用运动控制卡的 PSO 或硬件位置比较输出，避免 Windows 软件调度延迟影响触发位置。

## 动态设置曝光和增益

所有曝光时间统一使用微秒 `us`：

```csharp
ICameraParameterController parameters =
    registry.GetRequiredCapability<ICameraParameterController>("Camera.Top");

await parameters.SetExposureTimeAsync(12_000, cancellationToken); // 12 ms
await parameters.SetGainAsync(3.0, cancellationToken);
```

不要将毫秒值直接传入 `SetExposureTimeAsync()`。例如 `12 ms` 应写为 `12_000 us`。

## 海康专用配置

跨品牌通用参数放在 `CameraConfig`，厂商专属参数放在各厂商自己的配置类型中。

`HikCameraConfig` 当前增加：

| 属性 | 说明 |
| --- | --- |
| `ConfigureOptimalPacketSize` | GigE 相机连接时自动设置最佳网络包大小。 |
| `PixelFormat` | 海康 GenICam 像素格式名称。 |
| `AcquisitionFrameRate` | 启用并设置相机采集帧率。 |

例如：

```csharp
services.AddKwyHikVisionCamera(config =>
{
    config.DeviceId = "Camera.Top";
    config.SerialNumber = "TOP-SN";
    config.ConfigureOptimalPacketSize = true;
    config.PixelFormat = "Mono8";
    config.AcquisitionFrameRate = 60;
});
```

后续的 `DahuaCameraConfig`、`BaslerCameraConfig` 应分别保存各自厂商专用参数，不要把厂商参数字符串重新放回公共 `CameraConfig`。

## 停止与释放

```csharp
await frames.StopGrabbingAsync(cancellationToken);
await camera.DisconnectAsync(cancellationToken);
```

相机释放顺序为：

```text
取消帧接收任务
  -> 停止 SDK 取流
  -> 等待接收任务退出
  -> 关闭相机
  -> 释放 SDK 设备对象
```

使用依赖注入容器管理相机时，容器释放也会进入设备异步释放流程。

## 扩展新的相机品牌

新增大华或 Basler 模块时建议遵循以下结构：

```text
Kwy.Device.Cameras.Dahua
  DahuaCameraConfig : CameraConfig
  DahuaCameraDevice : CameraBase
  AddKwyDahuaCamera()

Kwy.Device.Cameras.Basler
  BaslerCameraConfig : CameraConfig
  BaslerCameraDevice : CameraBase
  AddKwyBaslerCamera()
```

实现原则：

1. 厂商 SDK 类型只能出现在厂商实现项目中。
2. 每帧在释放 SDK 缓冲区前转换为独立的 `CameraFrame`。
3. 只实现相机真正支持的能力接口。
4. 所有 SDK 返回码必须检查。
5. 连接中途失败必须关闭并释放已经创建的 SDK 对象。
6. 通过 `ICameraDevice` 多实例注册，由 `ICameraRegistry` 按 `DeviceId` 管理。
