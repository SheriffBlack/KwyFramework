namespace Kwy.Communicate.Abstractions;

/// <summary>
/// 为需要主动连接健康检查的协议提供可选配置。
/// </summary>
public interface IKeepAliveConfig
{
    /// <summary>
    /// 是否启用主动连接健康检查。
    /// </summary>
    bool KeepAlive { get; set; }

    /// <summary>
    /// 连接健康检查间隔，单位为毫秒。
    /// </summary>
    int KeepAliveInterval { get; set; }
}
