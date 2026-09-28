# Kwy.Device.Cameras.HikVision

`Kwy.Device.Cameras.HikVision` 是基于海康 `MvCameraControl.Net` SDK 实现的 Kwy 相机驱动模块。

## 当前支持

- GigE 和 USB 相机枚举。
- 通过序列号或 IP 地址选择相机。
- 连续采集、软件触发和外部硬件触发配置。
- 曝光时间、增益、像素格式和采集帧率配置。
- GigE 最佳网络包大小设置。
- 独立托管的 `CameraFrame` 图像数据。
- 多相机注册和按 `DeviceId` 查找。
- 连接失败回滚、停止取流和 SDK 资源释放。

完整的架构说明、依赖注入注册、连续采集、软硬件触发以及多相机使用方式，请参阅 [Kwy 相机模块设计与使用](../Kwy.Device.Abstractions/CAMERAS.md)。

## 注意事项

- 运行电脑需要安装与当前 DLL 匹配的海康 MVS Runtime 或相机 SDK 运行环境。
- 曝光时间统一使用微秒 `us`。
- 软件触发前必须先调用 `StartGrabbingAsync()`。
- `FrameArrived` 在帧接收线程触发，不要在事件处理器中执行耗时 UI 或算法操作。
- 当前构建与单元测试不代替真实相机、网卡、触发线和现场曝光条件下的硬件联调。
