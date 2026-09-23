using Kwy.Communicate.Abstractions.Enums;

namespace Kwy.Device.Abstractions.PLC;

/// <summary>
/// PLC 连接所使用的传输方式。
/// </summary>
public enum PlcConnectionTransport
{
    /// <summary>TCP 网络连接。</summary>
    Tcp,
    /// <summary>串口连接。</summary>
    Serial
}

/// <summary>
/// PLC 通用连接配置。厂商适配项目可继承此类并补充协议专属参数。
/// </summary>
public class PlcConfig : IDeviceConfig, IPlcKeepAliveConfig
{
    /// <summary>
    /// 获取或设置 PLC 连接传输方式。
    /// </summary>
    public PlcConnectionTransport Transport { get; set; } = PlcConnectionTransport.Tcp;

    /// <summary>
    /// 使用 TCP 连接时的 PLC IP 地址。
    /// </summary>
    public string IpAddress { get; set; } = "192.168.0.10";

    /// <summary>
    /// 目标 TCP 端口；为 0 时可由厂商适配器采用协议默认端口。
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// 使用串口连接时的端口名称。
    /// </summary>
    public string PortName { get; set; } = "COM1";

    /// <summary>
    /// 串口波特率。
    /// </summary>
    public int BaudRate { get; set; } = 9600;

    /// <summary>
    /// 串口数据位。
    /// </summary>
    public int DataBits { get; set; } = 8;

    /// <summary>
    /// 串口校验方式。
    /// </summary>
    public ParityType Parity { get; set; } = ParityType.None;

    /// <summary>
    /// 串口停止位。
    /// </summary>
    public StopBitsType StopBits { get; set; } = StopBitsType.One;

    /// <summary>
    /// 是否启用 PLC 协议层心跳。
    /// </summary>
    public bool KeepAlive { get; set; } = true;

    /// <summary>
    /// PLC 心跳间隔，单位为毫秒。
    /// </summary>
    public int KeepAliveInterval { get; set; } = 1000;

    /// <summary>
    /// PLC 心跳读取地址；为空时不执行主动心跳读取。
    /// </summary>
    public string? KeepAliveAddress { get; set; }

    /// <summary>
    /// 心跳地址的数据读取方式。
    /// </summary>
    public PlcKeepAliveMode KeepAliveMode { get; set; } = PlcKeepAliveMode.ReadBool;

    /// <summary>校验通用连接参数是否合法。</summary>
    public virtual bool Validate()
    {
        if (Transport == PlcConnectionTransport.Tcp && string.IsNullOrWhiteSpace(IpAddress))
        {
            return false;
        }

        if (Transport == PlcConnectionTransport.Serial)
        {
            if (string.IsNullOrWhiteSpace(PortName) || BaudRate <= 0 || DataBits is < 5 or > 8)
            {
                return false;
            }
        }

        if (KeepAlive && KeepAliveInterval <= 0)
        {
            return false;
        }

        return true;
    }
}
