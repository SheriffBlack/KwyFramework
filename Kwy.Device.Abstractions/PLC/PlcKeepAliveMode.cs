namespace Kwy.Device.Abstractions.PLC;

/// <summary>
/// PLC 心跳地址的数据读取方式。
/// </summary>
public enum PlcKeepAliveMode
{
    /// <summary>
    /// 按布尔值读取。
    /// </summary>
    ReadBool,

    /// <summary>
    /// 按 16 位有符号整数读取。
    /// </summary>
    ReadInt16,

    /// <summary>
    /// 按 32 位有符号整数读取。
    /// </summary>
    ReadInt32,

    /// <summary>
    /// 按 32 位单精度浮点数读取。
    /// </summary>
    ReadFloat,

    /// <summary>
    /// 按原始字节块读取。
    /// </summary>
    ReadBytes
}
