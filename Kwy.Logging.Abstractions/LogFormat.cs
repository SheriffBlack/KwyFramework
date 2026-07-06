namespace Kwy.Logging.Abstractions;

/// <summary>
/// 日志输出格式
/// </summary>
public enum LogFormat
{
    /// <summary>
    /// 同时写入文本和JSON格式（默认）
    /// </summary>
    Both,

    /// <summary>
    /// 仅写入文本格式（便于人工查看）
    /// </summary>
    TextOnly,

    /// <summary>
    /// 仅写入JSON格式（便于程序分析和查询）
    /// </summary>
    JsonOnly
}
