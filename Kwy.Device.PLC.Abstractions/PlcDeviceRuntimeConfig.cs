using Kwy.Device.Abstractions;

namespace Kwy.Device.PLC.Abstractions;

/// <summary>
/// PLC 设备的通用运行配置。
/// <para>仅包含与传输协议无关的设备级策略，例如协议层心跳；TCP、串口、ADS 和 OPC UA 的连接参数应由各自的配置模型承担。</para>
/// </summary>
public class PlcDeviceRuntimeConfig : IDeviceConfig, IPlcKeepAliveConfig
{
    /// <summary>是否启用 PLC 协议层心跳。</summary>
    public bool KeepAlive { get; set; } = true;

    /// <summary>PLC 心跳间隔，单位为毫秒。</summary>
    public int KeepAliveInterval { get; set; } = 1000;

    /// <summary>心跳读取使用的物理地址；为空时不执行主动读取。</summary>
    public string? KeepAliveAddress { get; set; }

    /// <summary>心跳地址的数据读取方式。</summary>
    public PlcKeepAliveMode KeepAliveMode { get; set; } = PlcKeepAliveMode.ReadBool;

    /// <summary>校验与协议无关的运行参数。</summary>
    public virtual bool Validate()
        => !KeepAlive || KeepAliveInterval > 0;
}
