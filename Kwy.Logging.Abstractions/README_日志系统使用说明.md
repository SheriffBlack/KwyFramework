# Kwy 日志系统使用说明

## 分层设计

日志模块分为两层：

```text
Kwy.Logging.Abstractions
  ILogService
  LogLevel
  LogFormat
  KwyLoggingOptions

Kwy.Logging.Serilog
  SerilogLogService
  AddKwySerilogLogging(...)
```

`Kwy.Logging.Abstractions` 不依赖 Serilog，也不依赖 Kwy.MVVM。通信层、设备层、业务层只需要依赖抽象层即可。

`Kwy.Logging.Serilog` 是基于 Serilog 的默认实现，负责文件输出、JSON 输出、日志作用域和 DI 注册。

## 注册

在业务项目或 Shell 项目的 `RegisterTypes(IServiceCollection services)` 中注册：

```csharp
using Kwy.Logging.Abstractions;
using Kwy.Logging.Serilog;

protected override void RegisterTypes(IServiceCollection services)
{
    services.AddKwySerilogLogging(options =>
    {
        options.MinimumLevel = LogLevel.Info;
        options.LogDirectory = "logs";
        options.FileNamePrefix = "app";
        options.RetainedFileCountLimit = 30;
        options.EnableTextFile = true;
        options.EnableJsonFile = true;
    });
}
```

如果使用默认配置：

```csharp
services.AddKwySerilogLogging();
```

默认会输出到：

```text
{AppContext.BaseDirectory}/logs/app-YYYYMMDD.txt
{AppContext.BaseDirectory}/logs/app-YYYYMMDD.json
```

## 注入使用

```csharp
public sealed class MyService
{
    private readonly ILogService log;

    public MyService(ILogService log)
    {
        this.log = log;
    }

    public void Run(string stationName)
    {
        log.Info("工站 {StationName} 开始运行", stationName);
    }
}
```

## 结构化日志

推荐使用消息模板，而不是字符串拼接：

```csharp
log.Info("用户 {UserId} 执行了操作 {Action}", userId, actionName);
```

异常日志：

```csharp
try
{
    // business code
}
catch (Exception ex)
{
    log.Error("处理订单 {OrderId} 失败", ex, orderId);
}
```

## 日志作用域

作用域内的日志会自动带上上下文属性：

```csharp
using (log.BeginScope(new { Station = "A01", TraceId = Guid.NewGuid() }))
{
    log.Info("开始测试");
    log.Info("测试完成");
}
```

也可以写单个属性：

```csharp
using (log.BeginScope("Operation", "Login"))
{
    log.Info("用户登录");
}
```

## 日志级别

日志级别从低到高：

```text
Trace
Debug
Info
Warning
Error
Fatal
None
```

运行时可调整级别：

```csharp
log.SetLevel(LogLevel.Warning);
```

判断某个级别是否启用：

```csharp
if (log.IsEnabled(LogLevel.Debug))
{
    log.Debug("调试数据: {Value}", value);
}
```

`LogLevel.None` 会禁用所有日志。

## 输出格式

默认 `LogFormat.Both`，同时写入文本和 JSON。

只写文本：

```csharp
log.Warning("人工查看的提示", LogFormat.TextOnly);
```

只写 JSON：

```csharp
log.Info("测试结果 {@Result}", LogFormat.JsonOnly, result);
```

## 配置项

`KwyLoggingOptions` 常用属性：

```csharp
public sealed class KwyLoggingOptions
{
    public LogLevel MinimumLevel { get; set; }
    public string LogDirectory { get; set; }
    public string FileNamePrefix { get; set; }
    public int? RetainedFileCountLimit { get; set; }
    public bool EnableTextFile { get; set; }
    public bool EnableJsonFile { get; set; }
    public bool SharedFile { get; set; }
    public string TextOutputTemplate { get; set; }
    public string? ApplicationName { get; set; }
}
```

业务项目可以从自己的配置文件读取后映射到 `KwyLoggingOptions`。日志模块不直接读取业务配置文件，也不依赖业务层的 `MachineConfig`。
