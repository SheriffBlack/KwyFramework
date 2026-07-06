namespace Kwy.Logging.Abstractions;


/// <summary>
/// 日志服务接口
/// 支持结构化日志、日志作用域和上下文信息
/// </summary>
public interface ILogService
{
    /// <summary>
    /// 记录信息级别日志
    /// </summary>
    void Info(string message);

    /// <summary>
    /// 记录信息级别日志（结构化日志）
    /// </summary>
    /// <param name="message">消息模板，支持占位符，如 "用户 {UserId} 执行了操作 {Action}"</param>
    /// <param name="args">占位符参数</param>
    void Info(string message, params object[] args);

    /// <summary>
    /// 记录信息级别日志（指定输出格式）
    /// </summary>
    /// <param name="message">消息</param>
    /// <param name="format">输出格式</param>
    void Info(string message, LogFormat format);

    /// <summary>
    /// 记录信息级别日志（结构化日志，指定输出格式）
    /// </summary>
    void Info(string message, LogFormat format, params object[] args);

    /// <summary>
    /// 记录警告级别日志
    /// </summary>
    void Warning(string message);

    /// <summary>
    /// 记录警告级别日志（结构化日志）
    /// </summary>
    void Warning(string message, params object[] args);

    /// <summary>
    /// 记录警告级别日志（指定输出格式）
    /// </summary>
    void Warning(string message, LogFormat format);

    /// <summary>
    /// 记录警告级别日志（结构化日志，指定输出格式）
    /// </summary>
    void Warning(string message, LogFormat format, params object[] args);

    /// <summary>
    /// 记录错误级别日志
    /// </summary>
    void Error(string message, Exception? ex = null);

    /// <summary>
    /// 记录错误级别日志（结构化日志，带异常）
    /// </summary>
    void Error(string message, Exception ex, params object[] args);

    /// <summary>
    /// 记录错误级别日志（结构化日志，无异常）
    /// </summary>
    void Error(string message, params object[] args);

    /// <summary>
    /// 记录错误级别日志（指定输出格式）
    /// </summary>
    /// <param name="message">消息</param>
    /// <param name="format">输出格式</param>
    /// <param name="ex">异常（可选）</param>
    void Error(string message, LogFormat format, Exception? ex = null);

    /// <summary>
    /// 记录错误级别日志（结构化日志，指定输出格式，带异常）
    /// </summary>
    void Error(string message, LogFormat format, Exception ex, params object[] args);

    /// <summary>
    /// 记录错误级别日志（结构化日志，指定输出格式，无异常）
    /// </summary>
    void Error(string message, LogFormat format, params object[] args);

    /// <summary>
    /// 记录调试级别日志
    /// </summary>
    void Debug(string message);

    /// <summary>
    /// 记录调试级别日志（结构化日志）
    /// </summary>
    void Debug(string message, params object[] args);

    /// <summary>
    /// 记录调试级别日志（指定输出格式）
    /// </summary>
    void Debug(string message, LogFormat format);

    /// <summary>
    /// 记录调试级别日志（结构化日志，指定输出格式）
    /// </summary>
    void Debug(string message, LogFormat format, params object[] args);

    /// <summary>
    /// 记录跟踪级别日志（最详细的日志）
    /// </summary>
    void Trace(string message);

    /// <summary>
    /// 记录跟踪级别日志（结构化日志）
    /// </summary>
    void Trace(string message, params object[] args);

    /// <summary>
    /// 记录跟踪级别日志（指定输出格式）
    /// </summary>
    void Trace(string message, LogFormat format);

    /// <summary>
    /// 记录跟踪级别日志（结构化日志，指定输出格式）
    /// </summary>
    void Trace(string message, LogFormat format, params object[] args);

    /// <summary>
    /// 记录致命错误级别日志（应用即将崩溃）
    /// </summary>
    void Fatal(string message, Exception? ex = null);

    /// <summary>
    /// 记录致命错误级别日志（结构化日志）
    /// </summary>
    void Fatal(string message, Exception ex, params object[] args);

    /// <summary>
    /// 记录致命错误级别日志（指定输出格式）
    /// </summary>
    void Fatal(string message, LogFormat format, Exception? ex = null);

    /// <summary>
    /// 记录致命错误级别日志（结构化日志，指定输出格式）
    /// </summary>
    void Fatal(string message, LogFormat format, Exception ex, params object[] args);

    /// <summary>
    /// 获取当前日志级别
    /// </summary>
    LogLevel GetCurrentLevel();

    /// <summary>
    /// 设置日志级别
    /// </summary>
    void SetLevel(LogLevel level);

    /// <summary>
    /// 检查指定日志级别是否启用
    /// </summary>
    bool IsEnabled(LogLevel level);

    /// <summary>
    /// 创建日志作用域（用于在作用域内自动添加上下文信息）
    /// </summary>
    /// <param name="properties">作用域属性，如 new { UserId = 123, Operation = "Login" }</param>
    /// <returns>可释放的作用域对象</returns>
    IDisposable BeginScope(object properties);

    /// <summary>
    /// 为后续日志添加上下文属性（直到作用域结束）
    /// </summary>
    /// <param name="key">属性键</param>
    /// <param name="value">属性值</param>
    /// <returns>可释放的作用域对象</returns>
    IDisposable BeginScope(string key, object value);
}