using Kwy.Logging.Abstractions;
using Serilog;
using Serilog.Context;
using Serilog.Core;
using Serilog.Events;

namespace Kwy.Logging.Serilog;

/// <summary>
/// Serilog implementation of the Kwy logging service.
/// </summary>
public sealed class SerilogLogService : ILogService, IDisposable
{
    private readonly ILogger logger;
    private readonly LoggingLevelSwitch levelSwitch;
    private LogLevel currentLevel;
    private bool disposed;

    public SerilogLogService(KwyLoggingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        currentLevel = options.MinimumLevel;
        levelSwitch = new LoggingLevelSwitch(ToSerilogLevel(options.MinimumLevel));

        string logFolder = ResolveLogDirectory(options.LogDirectory);
        Directory.CreateDirectory(logFolder);

        logger = CreateLogger(options, logFolder);
    }

    public LogLevel GetCurrentLevel()
    {
        return currentLevel;
    }

    public void SetLevel(LogLevel level)
    {
        currentLevel = level;
        levelSwitch.MinimumLevel = ToSerilogLevel(level);
        logger.Information("日志级别已设置为: {LogLevel}", level);
    }

    public bool IsEnabled(LogLevel level)
    {
        if (level == LogLevel.None || currentLevel == LogLevel.None)
        {
            return false;
        }

        return logger.IsEnabled(ToSerilogLevel(level));
    }

    public void Info(string message)
    {
        if (IsEnabled(LogLevel.Info))
        {
            logger.Information(message);
        }
    }

    public void Info(string message, params object[] args)
    {
        if (IsEnabled(LogLevel.Info))
        {
            logger.Information(message, args);
        }
    }

    public void Info(string message, LogFormat format)
    {
        if (IsEnabled(LogLevel.Info))
        {
            GetLoggerByFormat(format).Information(message);
        }
    }

    public void Info(string message, LogFormat format, params object[] args)
    {
        if (IsEnabled(LogLevel.Info))
        {
            GetLoggerByFormat(format).Information(message, args);
        }
    }

    public void Warning(string message)
    {
        if (IsEnabled(LogLevel.Warning))
        {
            logger.Warning(message);
        }
    }

    public void Warning(string message, params object[] args)
    {
        if (IsEnabled(LogLevel.Warning))
        {
            logger.Warning(message, args);
        }
    }

    public void Warning(string message, LogFormat format)
    {
        if (IsEnabled(LogLevel.Warning))
        {
            GetLoggerByFormat(format).Warning(message);
        }
    }

    public void Warning(string message, LogFormat format, params object[] args)
    {
        if (IsEnabled(LogLevel.Warning))
        {
            GetLoggerByFormat(format).Warning(message, args);
        }
    }

    public void Error(string message, Exception? ex = null)
    {
        if (IsEnabled(LogLevel.Error))
        {
            logger.Error(ex, message);
        }
    }

    public void Error(string message, Exception ex, params object[] args)
    {
        if (IsEnabled(LogLevel.Error))
        {
            logger.Error(ex, message, args);
        }
    }

    public void Error(string message, params object[] args)
    {
        if (IsEnabled(LogLevel.Error))
        {
            logger.Error(message, args);
        }
    }

    public void Error(string message, LogFormat format, Exception? ex = null)
    {
        if (!IsEnabled(LogLevel.Error))
        {
            return;
        }

        var formatLogger = GetLoggerByFormat(format);
        if (ex == null)
        {
            formatLogger.Error(message);
            return;
        }

        formatLogger.Error(ex, message);
    }

    public void Error(string message, LogFormat format, Exception ex, params object[] args)
    {
        if (IsEnabled(LogLevel.Error))
        {
            GetLoggerByFormat(format).Error(ex, message, args);
        }
    }

    public void Error(string message, LogFormat format, params object[] args)
    {
        if (IsEnabled(LogLevel.Error))
        {
            GetLoggerByFormat(format).Error(message, args);
        }
    }

    public void Debug(string message)
    {
        if (IsEnabled(LogLevel.Debug))
        {
            logger.Debug(message);
        }
    }

    public void Debug(string message, params object[] args)
    {
        if (IsEnabled(LogLevel.Debug))
        {
            logger.Debug(message, args);
        }
    }

    public void Debug(string message, LogFormat format)
    {
        if (IsEnabled(LogLevel.Debug))
        {
            GetLoggerByFormat(format).Debug(message);
        }
    }

    public void Debug(string message, LogFormat format, params object[] args)
    {
        if (IsEnabled(LogLevel.Debug))
        {
            GetLoggerByFormat(format).Debug(message, args);
        }
    }

    public void Trace(string message)
    {
        if (IsEnabled(LogLevel.Trace))
        {
            logger.Verbose(message);
        }
    }

    public void Trace(string message, params object[] args)
    {
        if (IsEnabled(LogLevel.Trace))
        {
            logger.Verbose(message, args);
        }
    }

    public void Trace(string message, LogFormat format)
    {
        if (IsEnabled(LogLevel.Trace))
        {
            GetLoggerByFormat(format).Verbose(message);
        }
    }

    public void Trace(string message, LogFormat format, params object[] args)
    {
        if (IsEnabled(LogLevel.Trace))
        {
            GetLoggerByFormat(format).Verbose(message, args);
        }
    }

    public void Fatal(string message, Exception? ex = null)
    {
        if (IsEnabled(LogLevel.Fatal))
        {
            logger.Fatal(ex, message);
        }
    }

    public void Fatal(string message, Exception ex, params object[] args)
    {
        if (IsEnabled(LogLevel.Fatal))
        {
            logger.Fatal(ex, message, args);
        }
    }

    public void Fatal(string message, LogFormat format, Exception? ex = null)
    {
        if (!IsEnabled(LogLevel.Fatal))
        {
            return;
        }

        var formatLogger = GetLoggerByFormat(format);
        if (ex == null)
        {
            formatLogger.Fatal(message);
            return;
        }

        formatLogger.Fatal(ex, message);
    }

    public void Fatal(string message, LogFormat format, Exception ex, params object[] args)
    {
        if (IsEnabled(LogLevel.Fatal))
        {
            GetLoggerByFormat(format).Fatal(ex, message, args);
        }
    }

    public IDisposable BeginScope(object properties)
    {
        ArgumentNullException.ThrowIfNull(properties);

        var disposable = new CompositeDisposable();
        foreach (var property in properties.GetType().GetProperties())
        {
            disposable.Add(LogContext.PushProperty(property.Name, property.GetValue(properties)));
        }

        return disposable;
    }

    public IDisposable BeginScope(string key, object value)
    {
        return LogContext.PushProperty(key, value);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        (logger as Logger)?.Dispose();
        disposed = true;
    }

    private ILogger CreateLogger(KwyLoggingOptions options, string logFolder)
    {
        var configuration = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(levelSwitch)
            .Enrich.FromLogContext()
            .Enrich.WithThreadId()
            .Enrich.WithProperty("MachineName", Environment.MachineName);

        if (!string.IsNullOrWhiteSpace(options.ApplicationName))
        {
            configuration.Enrich.WithProperty("Application", options.ApplicationName);
        }

        if (options.EnableTextFile)
        {
            configuration.WriteTo.Logger(lc => lc
                .Filter.ByExcluding(evt => HasLogFormat(evt, LogFormat.JsonOnly))
                .WriteTo.File(
                    path: Path.Combine(logFolder, $"{options.FileNamePrefix}-.txt"),
                    outputTemplate: options.TextOutputTemplate,
                    shared: options.SharedFile,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: options.RetainedFileCountLimit));
        }

        if (options.EnableJsonFile)
        {
            configuration.WriteTo.Logger(lc => lc
                .Filter.ByExcluding(evt => HasLogFormat(evt, LogFormat.TextOnly))
                .WriteTo.File(
                    path: Path.Combine(logFolder, $"{options.FileNamePrefix}-.json"),
                    formatter: new global::Serilog.Formatting.Json.JsonFormatter(renderMessage: true),
                    shared: options.SharedFile,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: options.RetainedFileCountLimit));
        }

        return configuration.CreateLogger();
    }

    private ILogger GetLoggerByFormat(LogFormat format)
    {
        return format == LogFormat.Both ? logger : logger.ForContext("LogFormat", format.ToString());
    }

    private static bool HasLogFormat(LogEvent logEvent, LogFormat format)
    {
        return logEvent.Properties.TryGetValue("LogFormat", out var value)
            && string.Equals(value.ToString(), $"\"{format}\"", StringComparison.Ordinal);
    }

    private static string ResolveLogDirectory(string logDirectory)
    {
        return Path.IsPathRooted(logDirectory)
            ? logDirectory
            : Path.Combine(AppContext.BaseDirectory, logDirectory);
    }

    private static LogEventLevel ToSerilogLevel(LogLevel level)
    {
        return level switch
        {
            LogLevel.Trace => LogEventLevel.Verbose,
            LogLevel.Debug => LogEventLevel.Debug,
            LogLevel.Info => LogEventLevel.Information,
            LogLevel.Warning => LogEventLevel.Warning,
            LogLevel.Error => LogEventLevel.Error,
            LogLevel.Fatal => LogEventLevel.Fatal,
            LogLevel.None => LogEventLevel.Fatal,
            _ => LogEventLevel.Information
        };
    }

    private static LogLevel FromSerilogLevel(LogEventLevel level)
    {
        return level switch
        {
            LogEventLevel.Verbose => LogLevel.Trace,
            LogEventLevel.Debug => LogLevel.Debug,
            LogEventLevel.Information => LogLevel.Info,
            LogEventLevel.Warning => LogLevel.Warning,
            LogEventLevel.Error => LogLevel.Error,
            LogEventLevel.Fatal => LogLevel.Fatal,
            _ => LogLevel.Info
        };
    }

    private sealed class CompositeDisposable : IDisposable
    {
        private readonly List<IDisposable> disposables = new();

        public void Add(IDisposable disposable)
        {
            disposables.Add(disposable);
        }

        public void Dispose()
        {
            foreach (var disposable in disposables)
            {
                disposable.Dispose();
            }

            disposables.Clear();
        }
    }
}
