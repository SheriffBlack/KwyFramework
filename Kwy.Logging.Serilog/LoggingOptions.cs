using Microsoft.Extensions.Logging;

namespace Kwy.Logging.Serilog;

/// <summary>Kwy Serilog 文件日志集成选项。</summary>
public sealed class LoggingOptions
{
    /// <summary>获取或设置最低日志级别。</summary>
    public LogLevel MinimumLevel { get; set; } = LogLevel.Information;

    /// <summary>获取或设置日志目录；相对路径以应用程序基目录为起点。</summary>
    public string LogDirectory { get; set; } = "logs";

    /// <summary>获取或设置日志文件名前缀。</summary>
    public string FileNamePrefix { get; set; } = "app";

    /// <summary>获取或设置保留的滚动日志文件数；为 <see langword="null"/> 时不限制。</summary>
    public int? RetainedFileCountLimit { get; set; } = 30;

    /// <summary>获取或设置是否输出文本日志。</summary>
    public bool EnableTextFile { get; set; } = true;

    /// <summary>获取或设置是否输出 JSON 日志。</summary>
    public bool EnableJsonFile { get; set; } = true;

    /// <summary>获取或设置是否允许多进程共享日志文件。</summary>
    public bool SharedFile { get; set; } = true;

    /// <summary>获取或设置文本日志输出模板。</summary>
    public string TextOutputTemplate { get; set; }
        = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u4}] [{ThreadId}] {Message:lj}{NewLine}{Exception}";

    /// <summary>获取或设置写入结构化日志的应用名称。</summary>
    public string? ApplicationName { get; set; } = AppDomain.CurrentDomain.FriendlyName;

    /// <summary>验证当前配置是否有效。</summary>
    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(LogDirectory))
        {
            throw new ArgumentException("日志目录不能为空。", nameof(LogDirectory));
        }

        if (EnableTextFile && string.IsNullOrWhiteSpace(TextOutputTemplate))
        {
            throw new ArgumentException("启用文本日志时，输出模板不能为空。", nameof(TextOutputTemplate));
        }
    }
}
