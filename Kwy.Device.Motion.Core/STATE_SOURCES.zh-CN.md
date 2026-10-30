# 运动状态采集源

`MotionStateMonitor` 只负责缓存 `MotionAxisSnapshot`、检查初始快照与发布统一事件。它不再决定设备如何获取状态。

## Core 内置状态源

- `PollingMotionStateSource`：`PeriodicTimer` + SDK 批量读取，默认适用于普通 PCI/PCIe 运动卡。
- `DedicatedThreadMotionStateSource`：专用后台线程 + SDK 批量读取，用于阻塞式 SDK、较高频轮询或需要隔离线程池抖动的设备。

两者共用 `AxisSnapshotCaptureBuffer`，依次优先使用：

1. `IBufferedAxisSnapshotReader`
2. `IBulkAxisSnapshotReader`
3. `IAxisSnapshotReader`

因此厂商适配器无需重复实现轮询、缓冲区和降级逻辑。

```csharp
options.AcquisitionMode = MotionStateAcquisitionMode.Polling;
options.PollInterval = TimeSpan.FromMilliseconds(50);
```

需要专用线程时只更改配置：

```csharp
options.AcquisitionMode = MotionStateAcquisitionMode.DedicatedThread;
options.PollInterval = TimeSpan.FromMilliseconds(5);
```

## 厂商状态源

ACS 回调、EtherCAT PDO、PLC 状态区和高端控制器混合采集应当由对应厂商项目继承 `MotionStateSourceBase`。

厂商实现只负责：

- 注册与解除厂商回调，或连接 PDO/PLC 状态区。
- 将原始数据转换为 `MotionAxisSnapshot`。
- 通过 `PublishSnapshot` 发布快照。
- 通过 `PublishError` 发布通信或转换错误。

`StartAsync` 成功返回前必须发布所有受监视轴的初始快照。这是自动模式的安全门，不能只注册回调后立即返回。

混合状态源应在厂商适配器内合并快速位置数据与低频诊断数据，然后发布一份完整快照。不要让两个独立状态源互相覆盖新旧数据。
