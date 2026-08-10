using Kwy.Communicate.Abstractions.Enums;

using Kwy.Communicate.Abstractions;

namespace Kwy.Communicate.TcpSerial.Configs;

/// <summary>
/// 串口通信协议配置
/// 升级为强类型枚举，支持全文本 JSON 存储
/// </summary>
public class SerialPortConfig : IProtocolConfig, IKeepAliveConfig
{
    /// <summary>
    /// 协议类型
    /// </summary>
    public ProtocolType ProtocolType => ProtocolType.SerialPort;

    /// <summary>
    /// 端口名称（如COM1, COM2等）
    /// </summary>
    public string Port { get; set; } = "COM1";

    /// <summary>
    /// 波特率
    /// </summary>
    public int BaudRate { get; set; } = 9600;

    /// <summary>
    /// 奇偶校验
    /// </summary>
    public ParityType Parity { get; set; } = ParityType.None;

    /// <summary>
    /// 数据位
    /// </summary>
    public int DataBits { get; set; } = 8;

    /// <summary>
    /// 停止位
    /// </summary>
    public StopBitsType StopBits { get; set; } = StopBitsType.One;

    /// <summary>
    /// 握手协议
    /// </summary>
    public HandshakeType Handshake { get; set; } = HandshakeType.None;


    /// <summary>
    /// 读取超时时间（毫秒）（高频场景建议减小）
    /// </summary>
    public int ReadTimeout { get; set; } = 100;

    /// <summary>
    /// 写入超时时间（毫秒）（高频场景建议减小）
    /// </summary>
    public int WriteTimeout { get; set; } = 100;

    /// <summary>
    /// 是否启用主动健康检查。
    /// </summary>
    public bool KeepAlive { get; set; } = true;

    /// <summary>
    /// 主动健康检查间隔（毫秒）。
    /// </summary>
    public int KeepAliveInterval { get; set; } = 1000;

    /// <summary>
    /// 连接超时时间（毫秒）
    /// </summary>
    public int Timeout { get; set; } = 5000;

    /// <summary>
    /// 是否启用自动重连
    /// </summary>
    public bool AutoReconnect { get; set; } = true;

    /// <summary>
    /// 自动重连最大重试次数
    /// </summary>
    public int MaxReconnectAttempts { get; set; } = 3;

    /// <summary>
    /// 自动重连间隔（毫秒）
    /// </summary>
    public int ReconnectInterval { get; set; } = 1000;

    /// <summary>
    /// 验证配置是否有效
    /// </summary>
    public bool Validate()
    {
        if (string.IsNullOrWhiteSpace(Port))
            return false;

        if (BaudRate <= 0)
            return false;

        if (DataBits < 5 || DataBits > 8)
            return false;

        if (Timeout <= 0)
            return false;

        if (ReadTimeout < 0 || WriteTimeout < 0)
            return false;

        if (MaxReconnectAttempts < 0 || ReconnectInterval < 0)
            return false;

        if (KeepAlive && KeepAliveInterval <= 0)
            return false;

        return true;
    }
}
