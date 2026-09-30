using Kwy.Communicate.Abstractions;

namespace Kwy.Communicate.Visa;

/// <summary>消息型 VISA 资源的通用通信配置。</summary>
public class VisaConfig : IProtocolConfig, IKeepAliveConfig
{
    /// <summary>获取或设置标准 VISA 资源名称，例如 <c>GPIB0::23::INSTR</c>。</summary>
    public string ResourceName { get; set; } = string.Empty;

    /// <inheritdoc />
    public int Timeout { get; set; } = 10_000;

    /// <inheritdoc />
    public bool AutoReconnect { get; set; } = true;

    /// <inheritdoc />
    public int MaxReconnectAttempts { get; set; } = 3;

    /// <inheritdoc />
    public int ReconnectInterval { get; set; } = 1000;

    /// <inheritdoc />
    public bool KeepAlive { get; set; }

    /// <inheritdoc />
    public int KeepAliveInterval { get; set; } = 5000;

    /// <summary>获取或设置可选的连接健康检查命令，例如 <c>*STB?</c>。</summary>
    public string? KeepAliveCommand { get; set; }

    /// <summary>获取或设置发送命令及查询时自动追加的文本结束符。</summary>
    public string WriteTerminator { get; set; } = "\n";

    /// <summary>获取或设置是否启用 VISA 读取终止字符。</summary>
    public bool ReadTerminationEnabled { get; set; } = true;

    /// <summary>获取或设置 VISA 读取终止字符对应的字节。</summary>
    public byte ReadTerminationCharacter { get; set; } = (byte)'\n';

    /// <summary>获取或设置写入最后一个字节时是否发送 VISA END 信号。</summary>
    public bool SendEndEnabled { get; set; } = true;

    /// <summary>获取或设置单次查询允许接收的最大响应字节数。</summary>
    public int QueryBufferSize { get; set; } = 65_536;

    /// <inheritdoc />
    public virtual bool Validate()
        => !string.IsNullOrWhiteSpace(ResourceName)
            && ResourceName.Contains("::", StringComparison.Ordinal)
            && Timeout > 0
            && MaxReconnectAttempts >= 0
            && ReconnectInterval >= 0
            && (!KeepAlive || KeepAliveInterval > 0)
            && QueryBufferSize > 0;

    internal VisaConfig Snapshot()
        => new()
        {
            ResourceName = ResourceName.Trim(),
            Timeout = Timeout,
            AutoReconnect = AutoReconnect,
            MaxReconnectAttempts = MaxReconnectAttempts,
            ReconnectInterval = ReconnectInterval,
            KeepAlive = KeepAlive,
            KeepAliveInterval = KeepAliveInterval,
            KeepAliveCommand = KeepAliveCommand,
            WriteTerminator = WriteTerminator,
            ReadTerminationEnabled = ReadTerminationEnabled,
            ReadTerminationCharacter = ReadTerminationCharacter,
            SendEndEnabled = SendEndEnabled,
            QueryBufferSize = QueryBufferSize
        };
}