namespace Kwy.Device.PLC.Hsl.Configuration;

/// <summary>HSL PLC 的 TCP 连接参数。</summary>
public sealed class HslTcpConnectionConfig : HslConnectionConfig
{
    /// <summary>PLC 主机名或 IP 地址。</summary>
    public string Host { get; set; } = "192.168.0.10";

    /// <summary>目标 TCP 端口；为 0 时由具体 PLC 协议采用默认端口。</summary>
    public int Port { get; set; }

    /// <inheritdoc />
    public override string Endpoint => Port > 0 ? $"{Host}:{Port}" : Host;

    /// <inheritdoc />
    public override bool Validate()
        => !string.IsNullOrWhiteSpace(Host) && Port is >= 0 and <= ushort.MaxValue;
}
