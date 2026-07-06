using Kwy.Communicate.Abstractions.Enums;

using Kwy.Communicate.Abstractions;

namespace Kwy.Communicate.TcpSerial.Configs;

/// <summary>
/// TCP/IP协议配置
/// </summary>
public class TcpConfig : IProtocolConfig, IKeepAliveConfig
{
    /// <summary>
    /// 协议类型
    /// </summary>
    public ProtocolType ProtocolType => ProtocolType.Tcp;

    /// <summary>
    /// 主机地址或IP地址
    /// </summary>
    public string Host { get; set; } = "127.0.0.1";

    /// <summary>
    /// 端口号
    /// </summary>
    public int Port { get; set; } = 8080;

    /// <summary>
    /// 接收缓冲区大小（高频场景建议增大）
    /// </summary>
    public int ReceiveBufferSize { get; set; } = 65536; // 64KB，适配高频读写

    /// <summary>
    /// 发送缓冲区大小（高频场景建议增大）
    /// </summary>
    public int SendBufferSize { get; set; } = 65536; // 64KB，适配高频读写

    /// <summary>
    /// 是否启用KeepAlive
    /// </summary>
    public bool KeepAlive { get; set; } = true;

    /// <summary>
    /// KeepAlive间隔（毫秒）
    /// </summary>
    public int KeepAliveInterval { get; set; } = 1000;

    /// <summary>
    /// 连接超时时间（毫秒）
    /// </summary>
    public int Timeout { get; set; } = 5000;

    /// <summary>
    /// 接收超时时间（毫秒）
    /// </summary>
    public int ReceiveTimeout { get; set; } = 30000;

    /// <summary>
    /// 发送超时时间（毫秒）
    /// </summary>
    public int SendTimeout { get; set; } = 30000;

    /// <summary>
    /// 是否启用自动重连
    /// </summary>
    public bool AutoReconnect { get; set; } = true;

    /// <summary>
    /// 自动重连最大重试次数
    /// </summary>
    public int MaxReconnectAttempts { get; set; } = 5;

    /// <summary>
    /// 自动重连间隔（毫秒）
    /// </summary>
    public int ReconnectInterval { get; set; } = 2000;

    /// <summary>
    /// 验证配置是否有效
    /// </summary>
    public bool Validate()
    {
        if (string.IsNullOrWhiteSpace(Host))
            return false;

        if (Port < 1 || Port > 65535)
            return false;

        if (Timeout <= 0)
            return false;

        if (ReceiveTimeout < 0 || SendTimeout < 0)
            return false;

        if (MaxReconnectAttempts < 0 || ReconnectInterval < 0)
            return false;

        if (ReceiveBufferSize <= 0 || SendBufferSize <= 0)
            return false;

        if (KeepAlive && KeepAliveInterval <= 0)
            return false;

        return true;
    }
}
