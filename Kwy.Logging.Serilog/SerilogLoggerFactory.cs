using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace Kwy.Logging.Serilog;

/// <summary>构建 Serilog 日志管道。</summary>
internal static class SerilogLoggerFactory
{
    /// <summary>根据日志选项创建 Serilog 日志器。</summary>
    internal static global::Serilog.ILogger CreateLogger(LoggingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        string logFolder = ResolveLogDirectory(options.LogDirectory);
        Directory.CreateDirectory(logFolder);

        var configuration = new LoggerConfiguration()
            .MinimumLevel.Is(ToSerilogLevel(options.MinimumLevel))
            .Enrich.FromLogContext()
            .Enrich.WithThreadId()
            .Enrich.WithProperty("MachineName", Environment.MachineName);

        if (options.MinimumLevel == LogLevel.None)
        {
            configuration.Filter.ByExcluding(static _ => true);
        }

        if (!string.IsNullOrWhiteSpace(options.ApplicationName))
        {
            configuration.Enrich.WithProperty("Application", options.ApplicationName);
        }

        ConfigureTextFile(configuration, options, logFolder);
        ConfigureJsonFile(configuration, options, logFolder);
        ConfigureOperatorTextFile(configuration, options);
        return configuration.CreateLogger();
    }

    private static void ConfigureTextFile(LoggerConfiguration configuration, LoggingOptions options, string logFolder)
    {
        if (!options.EnableTextFile)
        {
            return;
        }

        configuration.WriteTo.File(
            path: GetRollingFilePath(logFolder, options.FileNamePrefix, ".txt"),
            outputTemplate: options.TextOutputTemplate,
            shared: options.SharedFile,
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: options.RetainedFileCountLimit);
    }

    private static void ConfigureJsonFile(LoggerConfiguration configuration, LoggingOptions options, string logFolder)
    {
        if (!options.EnableJsonFile)
        {
            return;
        }

        configuration.WriteTo.File(
            path: GetRollingFilePath(logFolder, options.FileNamePrefix, ".json"),
            formatter: new global::Serilog.Formatting.Json.JsonFormatter(renderMessage: true),
            shared: options.SharedFile,
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: options.RetainedFileCountLimit);
    }

    private static void ConfigureOperatorTextFile(
        LoggerConfiguration configuration,
        LoggingOptions options)
    {
        if (!options.EnableOperatorTextFile)
        {
            return;
        }

        string operatorLogFolder = ResolveLogDirectory(options.OperatorLogDirectory);
        Directory.CreateDirectory(operatorLogFolder);
        string sourceContext = options.OperatorSourceContext!;

        configuration.WriteTo.Logger(nested => nested
            .Filter.ByIncludingOnly(logEvent => HasSourceContext(logEvent, sourceContext))
            .WriteTo.File(
                path: GetRollingFilePath(
                    operatorLogFolder,
                    options.OperatorFileNamePrefix,
                    ".txt"),
                outputTemplate: options.OperatorTextOutputTemplate,
                shared: options.SharedFile,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: options.RetainedFileCountLimit));
    }

    private static bool HasSourceContext(
        LogEvent logEvent,
        string expectedSourceContext)
        => logEvent.Properties.TryGetValue(
                "SourceContext",
                out LogEventPropertyValue? value)
            && value is ScalarValue { Value: string sourceContext }
            && string.Equals(
                sourceContext,
                expectedSourceContext,
                StringComparison.Ordinal);

    private static string GetRollingFilePath(string logFolder, string? fileNamePrefix, string extension)
    {
        string fileName = string.IsNullOrWhiteSpace(fileNamePrefix)
            ? extension
            : $"{fileNamePrefix}-{extension}";
        return Path.Combine(logFolder, fileName);
    }

    private static string ResolveLogDirectory(string logDirectory)
        => Path.IsPathRooted(logDirectory)
            ? logDirectory
            : Path.Combine(AppContext.BaseDirectory, logDirectory);

    private static LogEventLevel ToSerilogLevel(LogLevel level)
        => level switch
        {
            LogLevel.Trace => LogEventLevel.Verbose,
            LogLevel.Debug => LogEventLevel.Debug,
            LogLevel.Information => LogEventLevel.Information,
            LogLevel.Warning => LogEventLevel.Warning,
            LogLevel.Error => LogEventLevel.Error,
            LogLevel.Critical => LogEventLevel.Fatal,
            LogLevel.None => LogEventLevel.Fatal,
            _ => LogEventLevel.Information
        };
}
