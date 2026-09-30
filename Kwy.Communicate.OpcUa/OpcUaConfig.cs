using Kwy.Communicate.Abstractions;
using Opc.Ua;

namespace Kwy.Communicate.OpcUa;

/// <summary>OPC UA 客户端连接、认证、订阅和重连配置。</summary>
public sealed class OpcUaConfig : IProtocolConfig
{
    /// <summary>获取或设置 OPC UA 服务器端点地址。</summary>
    public string EndpointUrl { get; set; } = string.Empty;
    /// <summary>获取或设置安全策略名称或完整 URI。</summary>
    public string SecurityPolicy { get; set; } = SecurityPolicies.None;
    /// <summary>获取或设置消息安全模式。</summary>
    public MessageSecurityMode SecurityMode { get; set; } = MessageSecurityMode.None;
    /// <summary>获取或设置用户名；匿名认证时忽略。</summary>
    public string? Username { get; set; }
    /// <summary>获取或设置密码；匿名认证时忽略。</summary>
    public string? Password { get; set; }
    /// <summary>获取或设置是否使用匿名身份。</summary>
    public bool UseAnonymousIdentity { get; set; } = true;
    /// <summary>获取或设置会话超时时间（毫秒）。</summary>
    public uint SessionTimeout { get; set; } = 60_000;
    /// <inheritdoc />
    public int Timeout { get; set; } = 30_000;
    /// <inheritdoc />
    public bool AutoReconnect { get; set; } = true;
    /// <inheritdoc />
    public int MaxReconnectAttempts { get; set; } = 5;
    /// <inheritdoc />
    public int ReconnectInterval { get; set; } = 2_000;
    /// <summary>获取或设置 OPC UA 客户端应用名称。</summary>
    public string ApplicationName { get; set; } = "Kwy OPC UA Client";
    /// <summary>获取或设置 PKI 证书库根目录。</summary>
    public string PkiRootPath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kwy", "OpcUa", "pki");
    /// <summary>获取或设置订阅发布间隔（毫秒）。</summary>
    public int PublishingInterval { get; set; } = 100;
    /// <summary>获取或设置是否仅自动接受“不受信任”这一类证书错误。生产环境应保持为 false。</summary>
    public bool AutoAcceptUntrustedCertificates { get; set; }
    /// <summary>获取或设置连接后自动订阅、重连后恢复的节点。</summary>
    public List<string> SubscribeNodes { get; set; } = [];
    /// <summary>获取或设置消息缓冲区容量。</summary>
    public int MessageBufferCapacity { get; set; } = 10_000;
    /// <summary>获取或设置消息缓冲区满时的处理方式。</summary>
    public OpcUaMessageOverflowStrategy MessageOverflowStrategy { get; set; } = OpcUaMessageOverflowStrategy.DropOldest;

    /// <inheritdoc />
    public bool Validate()
    {
        if (!Uri.TryCreate(EndpointUrl, UriKind.Absolute, out Uri? uri)
            || !string.Equals(uri.Scheme, "opc.tcp", StringComparison.OrdinalIgnoreCase)
            || Timeout <= 0 || SessionTimeout == 0
            || MaxReconnectAttempts < 0 || ReconnectInterval < 0
            || PublishingInterval <= 0 || MessageBufferCapacity <= 0
            || string.IsNullOrWhiteSpace(ApplicationName) || string.IsNullOrWhiteSpace(PkiRootPath)
            || (!UseAnonymousIdentity && string.IsNullOrWhiteSpace(Username)))
            return false;

        string policyUri = ResolveSecurityPolicyUri(SecurityPolicy);
        bool noSecurity = SecurityMode == MessageSecurityMode.None;
        return !string.IsNullOrEmpty(policyUri)
            && noSecurity == string.Equals(policyUri, SecurityPolicies.None, StringComparison.Ordinal);
    }

    internal OpcUaConfig Snapshot()
        => new()
        {
            EndpointUrl = EndpointUrl.Trim(), SecurityPolicy = ResolveSecurityPolicyUri(SecurityPolicy),
            SecurityMode = SecurityMode, Username = Username, Password = Password,
            UseAnonymousIdentity = UseAnonymousIdentity, SessionTimeout = SessionTimeout, Timeout = Timeout,
            AutoReconnect = AutoReconnect, MaxReconnectAttempts = MaxReconnectAttempts,
            ReconnectInterval = ReconnectInterval, ApplicationName = ApplicationName.Trim(),
            PkiRootPath = Path.GetFullPath(PkiRootPath), PublishingInterval = PublishingInterval,
            AutoAcceptUntrustedCertificates = AutoAcceptUntrustedCertificates,
            SubscribeNodes = SubscribeNodes.Where(static node => !string.IsNullOrWhiteSpace(node))
                .Select(static node => node.Trim()).Distinct(StringComparer.Ordinal).ToList(),
            MessageBufferCapacity = MessageBufferCapacity, MessageOverflowStrategy = MessageOverflowStrategy
        };

    internal static string ResolveSecurityPolicyUri(string policy)
    {
        if (string.IsNullOrWhiteSpace(policy)) return string.Empty;
        if (policy.Contains('#', StringComparison.Ordinal)) return policy.Trim();
        return policy.Trim() switch
        {
            "None" => SecurityPolicies.None,
            "Basic256Sha256" => SecurityPolicies.Basic256Sha256,
            "Aes128_Sha256_RsaOaep" => SecurityPolicies.Aes128_Sha256_RsaOaep,
            "Aes256_Sha256_RsaPss" => SecurityPolicies.Aes256_Sha256_RsaPss,
            _ => string.Empty
        };
    }
}
