using Kwy.Communicate.Abstractions.Enums;

namespace Kwy.Device.PLC.Abstractions;

/// <summary>通过串口访问 PLC 的连接参数。</summary>
public sealed class SerialPlcConnectionConfig : PlcConnectionConfig
{
    /// <summary>串口名称，例如 COM6。</summary>
    public string PortName { get; set; } = "COM1";

    /// <summary>串口波特率。</summary>
    public int BaudRate { get; set; } = 9600;

    /// <summary>串口数据位。</summary>
    public int DataBits { get; set; } = 8;

    /// <summary>串口校验方式。</summary>
    public ParityType Parity { get; set; } = ParityType.None;

    /// <summary>串口停止位。</summary>
    public StopBitsType StopBits { get; set; } = StopBitsType.One;

    /// <inheritdoc />
    public override string Endpoint => $"{PortName}@{BaudRate},{DataBits},{Parity},{StopBits}";

    /// <inheritdoc />
    public override bool Validate()
        => !string.IsNullOrWhiteSpace(PortName)
            && BaudRate > 0
            && DataBits is >= 5 and <= 8;
}
