# Kwy 日志模块使用说明

[![NuGet](https://img.shields.io/nuget/v/Kwy.Logging.Serilog.svg)](https://www.nuget.org/packages/Kwy.Logging.Serilog)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](../LICENSES/MIT.txt)

## 设计

业务项目统一使用 `Microsoft.Extensions.Logging.Abstractions` 提供的 `ILogger<T>`、`LogLevel` 和 `LoggerMessage`。不再定义 Kwy 自己的日志接口。

`Kwy.Logging.Serilog` 是应用层的 Serilog 集成包，负责：

- 把 Serilog 注册为 `Microsoft.Extensions.Logging` 的 Provider；
- 输出按天滚动的文本和 JSON 日志；
- 配置最低级别、保留数量、目录、文件名和应用名；
- 保留结构化日志属性和 `BeginScope` 上下文。

类库只需引用 `Microsoft.Extensions.Logging.Abstractions`，不应引用 `Kwy.Logging.Serilog`。只有最终应用负责选择和注册日志实现。

## 注册

```csharp
using Kwy.Logging.Serilog;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

services.AddSerilogLogging(options =>
{
    options.LogDirectory = Path.Combine("Logs", "Developer");
    options.FileNamePrefix = "app";
    options.RetainedFileCountLimit = 31;
    options.MinimumLevel = LogLevel.Information;
    options.ApplicationName = "KwyTemplate";
});
```

注册后，DI 容器可直接解析 `ILogger<T>` 和 `ILoggerFactory`。

## 常规使用

```csharp
using Microsoft.Extensions.Logging;

public sealed class DeviceService(ILogger<DeviceService> logger)
{
    public void Connect(string deviceId)
    {
        logger.LogInformation("Device {DeviceId} connected", deviceId);
    }

    public void ReportFailure(string deviceId, Exception exception)
    {
        logger.LogError(exception, "Device {DeviceId} failed", deviceId);
    }
}
```

占位符名称应表达业务含义，例如 `{DeviceId}`，不要写成 `{Value}`。这些名称会作为 JSON 日志字段保留，方便查询和聚合。

## 为什么不要使用字符串插值

不推荐：

```csharp
logger.LogDebug($"Read device {deviceId}, value {value}");
```

即使 `Debug` 级别被关闭，调用 `LogDebug` 前也必须先执行字符串插值：格式化 `deviceId` 和 `value`，并创建最终字符串。日志最后虽然被过滤，这些 CPU 和分配开销已经发生。同时，插值后只剩下一段文本，`DeviceId` 和 `Value` 的结构化字段也丢失了。

更好：

```csharp
logger.LogDebug("Read device {DeviceId}, value {Value}", deviceId, value);
```

消息模板与参数分开，Provider 可以保留结构化字段，并在输出时再格式化。不过，普通 `LogDebug` 扩展方法仍然需要创建 `object?[]` 参数数组，值类型参数还可能装箱，并且每次需要解析消息模板。

## 高性能日志：LoggerMessage

### 首选：源生成

.NET 6 及以上推荐使用 `[LoggerMessage]` 源生成器：

```csharp
using Microsoft.Extensions.Logging;

internal static partial class DeviceLog
{
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Device {DeviceId} connected to {Endpoint}")]
    internal static partial void Connected(
        ILogger logger,
        string deviceId,
        string endpoint);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Error,
        Message = "Device {DeviceId} communication failed")]
    internal static partial void CommunicationFailed(
        ILogger logger,
        string deviceId,
        Exception exception);
}
```

调用：

```csharp
DeviceLog.Connected(logger, deviceId, endpoint);
DeviceLog.CommunicationFailed(logger, deviceId, exception);
```

源生成方式在编译期生成强类型代码，其主要性能优势是：

- 消息模板只解析一次，不在每次调用时重复解析；
- 生成代码先检查 `IsEnabled`，被关闭的级别会快速返回；
- 使用强类型参数，避免普通扩展方法的 `object?[]` 分配；
- 避免大多数值类型装箱；
- 在编译期检查模板和方法签名，减少占位符与参数不匹配。

因此，它不只是“比字符串插值快”：它同时避免了插值字符串的无条件创建，以及普通结构化日志扩展方法的参数数组、装箱和重复模板解析开销。

### 动态级别

日志级别由运行时数据决定时，可将 `Level` 留给方法参数：

```csharp
[LoggerMessage(EventId = 1100, Message = "Machine state changed to {State}")]
internal static partial void StateChanged(
    ILogger logger,
    LogLevel level,
    string state);
```

### 兼容旧项目：LoggerMessage.Define

`LoggerMessage.Define` 会创建可缓存的强类型委托，也能避免每次解析模板和创建参数数组：

```csharp
private static readonly Action<ILogger, string, Exception?> DeviceConnected =
    LoggerMessage.Define<string>(
        LogLevel.Information,
        new EventId(1001, nameof(DeviceConnected)),
        "Device {DeviceId} connected");

DeviceConnected(logger, deviceId, null);
```

新代码优先使用 `[LoggerMessage]`；只有在不能使用源生成器时再使用 `Define`。

## 使用选择

| 场景 | 建议 |
|---|---|
| 低频的启动、关闭和管理操作 | 结构化 `LogInformation` 等扩展方法即可 |
| 轮询、通信、采样、图像帧等高频路径 | 使用 `[LoggerMessage]` |
| 计算日志参数本身很昂贵 | 先用 `logger.IsEnabled(level)` 判断 |
| 关联一次业务操作的多条日志 | 使用 `BeginScope` 加入关联 ID |

```csharp
if (logger.IsEnabled(LogLevel.Debug))
{
    string snapshot = BuildExpensiveSnapshot();
    logger.LogDebug("Machine snapshot: {Snapshot}", snapshot);
}
```

`LoggerMessage` 可以减少日志 API 自身开销，但无法阻止调用者在进入方法前已经执行的昂贵计算，因此后一种场景仍需要显式 `IsEnabled`。

## 日志级别

- `Trace`：极高频、逐步诊断信息。
- `Debug`：开发和现场调试信息。
- `Information`：正常生命周期和关键业务事件。
- `Warning`：已恢复或可继续运行的异常情况。
- `Error`：当前操作失败，但应用仍可运行。
- `Critical`：应用或核心子系统无法继续运行，对应 Serilog `Fatal`。

不要把正常的业务拒绝、用户输入错误或可预期的设备状态记为 `Error`。
