using Kwy.Communicate.Abstractions.Enums;

namespace Kwy.Communicate.Abstractions;

/// <summary>
/// 协议配置接口
/// </summary>
public interface IProtocolConfig
{
    /// <summary>
    /// 协议类型
    /// </summary>
    ProtocolType ProtocolType { get; }

    /// <summary>
    /// 连接超时时间（毫秒）
    /// </summary>
    int Timeout { get; set; }

    /// <summary>
    /// 是否启用自动重连
    /// </summary>
    bool AutoReconnect { get; set; }

    /// <summary>
    /// 自动重连最大重试次数
    /// </summary>
    int MaxReconnectAttempts { get; set; }

    /// <summary>
    /// 自动重连间隔（毫秒）
    /// </summary>
    int ReconnectInterval { get; set; }

    /// <summary>
    /// 验证配置是否有效
    /// </summary>
    /// <returns>如果配置有效返回true，否则返回false</returns>
    bool Validate();
}
