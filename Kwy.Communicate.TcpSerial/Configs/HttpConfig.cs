using Kwy.Communicate.Abstractions.Enums;

using Kwy.Communicate.Abstractions;

namespace Kwy.Communicate.TcpSerial.Configs;

/// <summary>
/// HTTP/HTTPS协议配置
/// </summary>
public class HttpConfig : IProtocolConfig
{
    /// <summary>
    /// 协议类型
    /// </summary>
    public ProtocolType ProtocolType => ProtocolType.Http;

    /// <summary>
    /// 请求URL
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// HTTP方法（GET, POST, PUT, DELETE等）
    /// </summary>
    public HttpMethod Method { get; set; } = HttpMethod.Get;

    /// <summary>
    /// 请求头
    /// </summary>
    public Dictionary<string, string> Headers { get; set; } = new();

    /// <summary>
    /// 请求内容类型
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// 连接超时时间（毫秒）
    /// </summary>
    public int Timeout { get; set; } = 30000;

    /// <summary>
    /// 是否启用SSL证书验证
    /// </summary>
    public bool ValidateCertificate { get; set; } = true;

    /// <summary>
    /// 是否启用自动重连
    /// </summary>
    public bool AutoReconnect { get; set; } = false;

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
        if (string.IsNullOrWhiteSpace(Url))
            return false;

        if (!Uri.TryCreate(Url, UriKind.Absolute, out var uri))
            return false;

        if (Timeout <= 0)
            return false;

        if (MaxReconnectAttempts < 0 || ReconnectInterval < 0)
            return false;

        return true;
    }
}
