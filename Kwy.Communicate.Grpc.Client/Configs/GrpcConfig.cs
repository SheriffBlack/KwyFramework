using Kwy.Communicate.Abstractions;
using Kwy.Communicate.Abstractions.Enums;
using Kwy.Communicate.Grpc.Client.Enums;

namespace Kwy.Communicate.Grpc.Client.Configs;

/// <summary>
/// gRPC 端点的配置
/// </summary>
public sealed class GrpcConfig : IProtocolConfig, IKeepAliveConfig
{
    public ProtocolType ProtocolType => ProtocolType.Grpc;

    /// <summary>
    /// 传输方式。默认为基于 HTTP/2 的 TCP。
    /// </summary>
    public GrpcTransport Transport { get; set; } = GrpcTransport.Tcp;

    /// <summary>
    /// 端点。
    /// Tcp：完整 HTTP/HTTPS 服务地址，例如 https://localhost:5001。
    /// NamedPipe：管道名称，例如 kwy-template。
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// 连接确认超时时间（毫秒）。由于 gRPC Channel 为惰性连接，
    /// 客户端会在该时间内完成一次 ConnectivityService.Ping 调用。
    /// 每个业务 RPC 应单独设置 Deadline。
    /// </summary>
    public int Timeout { get; set; } = 5_000;

    /// <summary>
    /// 是否启用自动重连。
    /// </summary>
    public bool AutoReconnect { get; set; } = true;

    /// <summary>
    /// 自动重连最大重试次数。
    /// </summary>
    public int MaxReconnectAttempts { get; set; } = 5;

    /// <summary>
    /// 自动重连间隔（毫秒）。
    /// </summary>
    public int ReconnectInterval { get; set; } = 2_000;

    /// <summary>
    /// 是否启用KeepAlive
    /// </summary>
    public bool KeepAlive { get; set; } = true;

    /// <summary>
    /// KeepAlive 间隔（毫秒）。每次检查会调用 ConnectivityService.Ping。
    /// </summary>
    public int KeepAliveInterval { get; set; } = 5_000;

    public bool Validate()
    {
        bool validEndpoint = Transport switch
        {
            GrpcTransport.Tcp => Uri.TryCreate(Endpoint, UriKind.Absolute, out Uri? endpoint)
                && (endpoint.Scheme == Uri.UriSchemeHttp || endpoint.Scheme == Uri.UriSchemeHttps),
            GrpcTransport.NamedPipe => OperatingSystem.IsWindows()
                && !string.IsNullOrWhiteSpace(Endpoint),
            _ => false
        };

        if (!validEndpoint)
        {
            return false;
        }

        return Timeout > 0
            && MaxReconnectAttempts >= 0
            && ReconnectInterval >= 0
            && (!KeepAlive || KeepAliveInterval > 0);
    }
}
