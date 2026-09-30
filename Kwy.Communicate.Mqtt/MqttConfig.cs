using Kwy.Communicate.Abstractions.Enums;

using Kwy.Communicate.Abstractions;

namespace Kwy.Communicate.Mqtt;

/// <summary>
/// MQTT协议配置
/// </summary>
public sealed class MqttConfig : IProtocolConfig
{
    /// <summary>
    /// MQTT代理服务器地址
    /// </summary>
    public string Host { get; set; } = "127.0.0.1";

    /// <summary>
    /// MQTT代理服务器端口
    /// </summary>
    public int Port { get; set; } = 1883;

    /// <summary>
    /// 客户端ID
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// 用户名
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// 密码
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// 是否使用TLS/SSL
    /// </summary>
    public bool UseTls { get; set; } = false;

    /// <summary>
    /// 订阅的主题列表
    /// </summary>
    public IReadOnlyCollection<string> SubscribeTopics { get; set; } = Array.Empty<string>();

    /// <summary>
    /// 发布主题（默认）
    /// </summary>
    public string? PublishTopic { get; set; }

    /// <summary>
    /// 保持连接时间（秒）
    /// </summary>
    public ushort KeepAlivePeriod { get; set; } = 60;

    /// <summary>
    /// 是否清除会话
    /// </summary>
    public bool CleanSession { get; set; } = true;

    /// <summary>
    /// 连接超时时间（毫秒）
    /// </summary>
    public int Timeout { get; set; } = 10_000;

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
    /// 服务质量等级（0-2）
    /// </summary>
    public byte QualityOfServiceLevel { get; set; } = 0;

    /// <summary>
    /// 是否接受任何服务器证书。此选项会禁用服务器身份验证，只应在受控测试环境使用。
    /// </summary>
    public bool DangerousAcceptAnyServerCertificate { get; set; }

    /// <summary>
    /// 消息队列缓冲区容量。默认 1000。
    /// </summary>
    public int MessageBufferCapacity { get; set; } = 1000;

    /// <summary>
    /// 消息缓冲区已满时的处理策略。
    /// </summary>
    public MqttMessageOverflowStrategy MessageOverflowStrategy { get; set; } = MqttMessageOverflowStrategy.DropOldest;

    /// <summary>
    /// 验证配置是否有效
    /// </summary>
    public bool Validate()
    {
        if (string.IsNullOrWhiteSpace(Host))
            return false;

        if (Port < 1 || Port > 65535)
            return false;

        if (string.IsNullOrWhiteSpace(ClientId))
            return false;

        if (Timeout <= 0)
            return false;

        if (MaxReconnectAttempts < 0 || ReconnectInterval < 0)
            return false;

        if (KeepAlivePeriod == 0)
            return false;

        if (QualityOfServiceLevel > 2)
            return false;

        if (MessageBufferCapacity <= 0)
            return false;

        if (!Enum.IsDefined(MessageOverflowStrategy))
            return false;

        if (SubscribeTopics == null
            || SubscribeTopics.Any(topic => !IsValidTopicFilter(topic)))
        {
            return false;
        }

        if (PublishTopic != null && !IsValidPublishTopic(PublishTopic))
            return false;

        return true;
    }

    internal MqttConfig Snapshot()
        => new()
        {
            Host = Host,
            Port = Port,
            ClientId = ClientId,
            Username = Username,
            Password = Password,
            UseTls = UseTls,
            SubscribeTopics = SubscribeTopics.ToArray(),
            PublishTopic = PublishTopic,
            KeepAlivePeriod = KeepAlivePeriod,
            CleanSession = CleanSession,
            Timeout = Timeout,
            AutoReconnect = AutoReconnect,
            MaxReconnectAttempts = MaxReconnectAttempts,
            ReconnectInterval = ReconnectInterval,
            QualityOfServiceLevel = QualityOfServiceLevel,
            DangerousAcceptAnyServerCertificate = DangerousAcceptAnyServerCertificate,
            MessageBufferCapacity = MessageBufferCapacity,
            MessageOverflowStrategy = MessageOverflowStrategy
        };

    internal static bool IsValidPublishTopic(string? topic)
        => !string.IsNullOrWhiteSpace(topic)
            && !topic.Contains('\0')
            && !topic.Contains('+')
            && !topic.Contains('#');

    internal static bool IsValidTopicFilter(string? topic)
        => !string.IsNullOrWhiteSpace(topic) && !topic.Contains('\0');
}
