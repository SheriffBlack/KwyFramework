namespace Kwy.Logging.Abstractions;

/// <summary>
/// Kwy logging configuration shared by logging implementations.
/// </summary>
public sealed class KwyLoggingOptions
{
    /// <summary>
    /// Gets or sets the minimum log level.
    /// </summary>
    public LogLevel MinimumLevel { get; set; } = LogLevel.Info;

    /// <summary>
    /// Gets or sets the log directory. Relative paths are resolved from AppContext.BaseDirectory.
    /// </summary>
    public string LogDirectory { get; set; } = "logs";

    /// <summary>
    /// Gets or sets the file name prefix.
    /// </summary>
    public string FileNamePrefix { get; set; } = "app";

    /// <summary>
    /// Gets or sets the number of retained rolling log files. Null keeps all files.
    /// </summary>
    public int? RetainedFileCountLimit { get; set; } = 30;

    /// <summary>
    /// Gets or sets whether text logs are written.
    /// </summary>
    public bool EnableTextFile { get; set; } = true;

    /// <summary>
    /// Gets or sets whether JSON logs are written.
    /// </summary>
    public bool EnableJsonFile { get; set; } = true;

    /// <summary>
    /// Gets or sets whether files can be shared by multiple processes.
    /// </summary>
    public bool SharedFile { get; set; } = true;

    /// <summary>
    /// Gets or sets the text log output template.
    /// </summary>
    public string TextOutputTemplate { get; set; }
        = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u4}] [{ThreadId}] {Message:lj}{NewLine}{Exception}";

    /// <summary>
    /// Gets or sets the application name written as a structured property.
    /// </summary>
    public string? ApplicationName { get; set; } = AppDomain.CurrentDomain.FriendlyName;
}
