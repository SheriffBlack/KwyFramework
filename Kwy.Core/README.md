# Kwy.Core

`Kwy.Core` 放 Kwy 框架的通用基础能力，不依赖 WPF、设备 SDK 或通信协议。

## Threading

`Kwy.Core.Threading` 提供统一后台任务和周期任务调度能力，用于减少项目中随意 `new Thread`、`Task.Run`、`Thread.Sleep` 和不可控轮询。

它不试图接管 .NET 线程池，也不是硬实时调度器。它的目标是：

- 统一周期任务入口。
- 统一停止周期任务。
- 统一上报后台异常。
- 记录任务运行状态、耗时和漂移。
- 避免 `while(true) + Thread.Sleep` 风格代码散落各处。

### 适合场景

- PLC 状态轮询。
- 设备健康检查。
- MES 重试上报。
- 日志批量刷新。
- 缓存清理。
- 低频设备采集。

### 不适合场景

- 运动控制插补。
- IO 精准触发。
- 相机硬触发。
- 毫秒级强实时采样。

这些应交给 PLC、运动控制卡、IO 卡、相机 SDK 或硬件触发机制。

## 后台异常上报

普通一次性任务继续直接使用 `Task.Run` 或 `await`。`Kwy.Core.Threading` 不替代所有 `Task.Run`。

如果需要把周期任务异常反馈到 UI，可以使用统一错误上报器：

```csharp
var reporter = new BackgroundTaskErrorReporter();
reporter.ErrorReported += (_, error) =>
{
    // UI 层可以在这里切回 UI 线程，显示弹窗、状态栏或写入日志面板。
    Console.WriteLine($"{error.Source}: {error.Exception.Message}");
};
```

## 周期任务

```csharp
var reporter = new BackgroundTaskErrorReporter();
IPeriodicTaskScheduler scheduler = new PeriodicTaskScheduler(reporter);

IPeriodicTaskHandle handle = scheduler.Start(
    "PLC状态轮询",
    TimeSpan.FromMilliseconds(100),
    async token =>
    {
        await plc.SyncStateAsync(token);
    },
    new PeriodicTaskOptions
    {
        Mode = PeriodicTaskMode.FixedDelay,
        ExceptionPolicy = PeriodicTaskExceptionPolicy.Continue
    });
```

停止：

```csharp
await handle.StopAsync();
```

## 周期模式

| 模式 | 说明 | 适合场景 |
| --- | --- | --- |
| `FixedDelay` | 每次执行完成后再等待一个周期，默认模式 | PLC 轮询、MES 重试、设备健康检查 |
| `PeriodicTimer` | 基于 .NET `PeriodicTimer`，异步友好 | 普通状态刷新、心跳、清理 |
| `FixedRate` | 基于 `Stopwatch` 校正理论触发点，减少累计漂移 | 对周期稳定性更敏感的后台轮询 |

`FixedRate` 只能减少软件调度漂移，不代表 Windows 具备硬实时能力。

## 状态与统计

`IPeriodicTaskHandle` 可查看：

- `IsRunning`
- `TickCount`
- `LastStartedAt`
- `LastCompletedAt`
- `LastExecutionTime`
- `LastCycleTime`
- `LastDrift`
- `LastException`

`LastExecutionTime` 表示任务体本身耗时。`LastCycleTime` 表示相邻两次执行开始之间的时间，通常可以理解为执行耗时 + 等待延时。

这些数据可用于调试、状态页或运行日志。

普通一次性后台任务不建议为了“统一”而强行包装。只有长期循环、轮询、状态同步这类任务，才建议通过 `PeriodicTaskScheduler` 管理。

## 专用长线程

默认情况下，`PeriodicTaskScheduler` 使用 async 任务循环，适合异步 IO 型轮询。

如果某些现场 SDK 是同步阻塞式调用，或者确实需要一个专用长线程做兜底软件轮询，可以显式启用：

```csharp
var handle = scheduler.Start(
    "IO边沿兜底轮询",
    TimeSpan.FromMilliseconds(1),
    token =>
    {
        io.ReadDiSnapshot64();
        return ValueTask.CompletedTask;
    },
    new PeriodicTaskOptions
    {
        Mode = PeriodicTaskMode.FixedRate,
        UseDedicatedThread = true,
        DedicatedThreadPriority = ThreadPriority.AboveNormal
    });
```

注意：

- 专用长线程只建议用于同步阻塞 SDK 或兜底低抖动轮询。
- 它仍然不是 Windows 硬实时能力。
- 不建议把普通异步 IO 任务都改成专用线程。
- COM STA、WPF Dispatcher、运动控制硬实时采样仍应使用专门机制。
