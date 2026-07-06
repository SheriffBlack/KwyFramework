namespace Kwy.Logging.Abstractions;

/// <summary>
/// 日志级别枚举（从低到高）
/// </summary>
public enum LogLevel
{
    /// <summary>
    /// 跟踪级别：最详细的日志，用于诊断问题
    /// </summary>
    Trace = 0,

    /// <summary>
    /// 调试级别：开发调试信息
    /// </summary>
    Debug = 1,

    /// <summary>
    /// 信息级别：一般信息性消息
    /// </summary>
    Info = 2,

    /// <summary>
    /// 警告级别：警告信息，不影响功能
    /// </summary>
    Warning = 3,

    /// <summary>
    /// 错误级别：错误信息，影响功能
    /// </summary>
    Error = 4,

    /// <summary>
    /// 致命级别：致命错误，应用可能崩溃
    /// </summary>
    Fatal = 5,

    /// <summary>
    /// 无日志：禁用所有日志
    /// </summary>
    None = 6
}
