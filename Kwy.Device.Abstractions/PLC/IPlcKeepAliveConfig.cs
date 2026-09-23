namespace Kwy.Device.Abstractions.PLC;

/// <summary>
/// PLC 协议层心跳配置。
/// </summary>
public interface IPlcKeepAliveConfig
{
    /// <summary>
    /// 是否启用 PLC 协议层心跳。
    /// </summary>
    bool KeepAlive { get; set; }

    /// <summary>
    /// PLC 心跳间隔，单位为毫秒。
    /// </summary>
    int KeepAliveInterval { get; set; }

    /// <summary>
    /// 心跳读取使用的物理地址；为空时不执行主动读取。
    /// </summary>
    string? KeepAliveAddress { get; set; }

    /// <summary>
    /// 心跳地址的数据读取方式。
    /// </summary>
    PlcKeepAliveMode KeepAliveMode { get; set; }
}
