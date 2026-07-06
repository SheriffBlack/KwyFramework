using Kwy.Communicate.Abstractions.Enums;

using Kwy.Communicate.Abstractions;

namespace Kwy.Communicate.OpcUa;

/// <summary>
/// OPC UA协议配置
/// </summary>
public class OpcUaConfig : IProtocolConfig
{
    /// <summary>
    /// 协议类型
    /// </summary>
    public ProtocolType ProtocolType => ProtocolType.OpcUa;

    /// <summary>
    /// OPC UA服务器端点URL
    /// </summary>
    public string EndpointUrl { get; set; } = string.Empty;

    /// <summary>
    /// 安全策略
    /// </summary>
    public string SecurityPolicy { get; set; } = "None";

    /// <summary>
    /// 安全模式（None, Sign, SignAndEncrypt）
    /// </summary>
    public string SecurityMode { get; set; } = "None";

    /// <summary>
    /// 用户名（可选）
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// 密码（可选）
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// 是否使用匿名身份验证
    /// </summary>
    public bool UseAnonymousIdentity { get; set; } = true;

    /// <summary>
    /// 会话超时时间（毫秒）
    /// </summary>
    public uint SessionTimeout { get; set; } = 60000;

    /// <summary>
    /// 连接超时时间（毫秒）
    /// </summary>
    public int Timeout { get; set; } = 30000;

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
    /// 应用程序名称
    /// </summary>
    public string ApplicationName { get; set; } = "Comm OPC UA Client";

    // ==========================================
    // 新增：针对订阅模式与工业现场调优的专属配置
    // ==========================================

    /// <summary>
    /// 默认订阅的发布间隔（毫秒）。
    /// 决定了底层设备向PC推送数据的最快频率，推荐设为 50-100ms。
    /// </summary>
    public int PublishingInterval { get; set; } = 100;

    /// <summary>
    /// 自动接受不受信任的证书（内网工控环境通常设为 true 以简化部署）
    /// </summary>
    public bool AutoAcceptUntrustedCertificates { get; set; } = true;

    /// <summary>
    /// 建立连接后，需要自动监听（订阅）的节点列表。
    /// 用于断线重连后快速恢复业务上下文。
    /// </summary>
    public List<string> SubscribeNodes { get; set; } = new List<string>();

    /// <summary>
    /// 消息队列缓冲区容量。默认 10000。
    /// </summary>
    public int MessageBufferCapacity { get; set; } = 10000;

    /// <summary>
    /// 验证配置是否有效
    /// </summary>
    public bool Validate()
    {
        if (string.IsNullOrWhiteSpace(EndpointUrl))
            return false;

        if (!Uri.TryCreate(EndpointUrl, UriKind.Absolute, out var uri))
            return false;

        if (Timeout <= 0)
            return false;

        if (SessionTimeout == 0)
            return false;

        if (MaxReconnectAttempts < 0 || ReconnectInterval < 0)
            return false;

        if (PublishingInterval <= 0 || MessageBufferCapacity <= 0)
            return false;

        if (!UseAnonymousIdentity && string.IsNullOrWhiteSpace(Username))
            return false;

        return true;
    }
}
