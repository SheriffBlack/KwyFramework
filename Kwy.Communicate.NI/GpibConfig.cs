using Kwy.Communicate.Abstractions.Enums;

using Kwy.Communicate.Abstractions;

namespace Kwy.Communicate.NI;

public class GpibConfig: IProtocolConfig, IKeepAliveConfig
{    
    /// <summary>
     /// 协议类型
     /// </summary>
    public ProtocolType ProtocolType => ProtocolType.Gpib;

    /// <summary>
    /// GPIB板卡号（通常为0）
    /// </summary>
    public int BoardNumber { get; set; } = 0;

    /// <summary>
    /// 主地址（0-30）
    /// </summary>
    public int PrimaryAddress { get; set; } = 1;

    /// <summary>
    /// 次地址（0-30，0表示不使用次地址）
    /// </summary>
    public int SecondaryAddress { get; set; } = 0;

    /// <summary>
    /// 超时时间（毫秒）
    /// </summary>
    public int Timeout { get; set; } = 10000;

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
    /// 是否启用主动健康检查。
    /// </summary>
    public bool KeepAlive { get; set; } = true;

    /// <summary>
    /// 主动健康检查间隔（毫秒）。
    /// </summary>
    public int KeepAliveInterval { get; set; } = 1000;

    /// <summary>
    /// 主动健康检查命令。为空时仅检查本地 GPIB 会话对象是否存在。
    /// </summary>
    public string? KeepAliveCommand { get; set; }

    /// <summary>
    /// 验证配置是否有效
    /// </summary>
    public bool Validate()
    {
        if (BoardNumber < 0)
            return false;

        if (PrimaryAddress < 0 || PrimaryAddress > 30)
            return false;

        if (SecondaryAddress < 0 || SecondaryAddress > 30)
            return false;

        if (Timeout <= 0)
            return false;

        if (KeepAlive && KeepAliveInterval <= 0)
            return false;

        return true;
    }
}
