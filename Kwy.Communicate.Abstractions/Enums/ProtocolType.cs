namespace Kwy.Communicate.Abstractions.Enums;

/// <summary>
/// 通信协议类型枚举
/// </summary>
public enum ProtocolType
{
    /// <summary>
    /// HTTP协议
    /// </summary>
    Http,

    /// <summary>
    /// TCP/IP协议
    /// </summary>
    Tcp,

    /// <summary>
    /// 串口通信协议
    /// </summary>
    SerialPort,

    /// <summary>
    /// GPIB协议
    /// </summary>
    Gpib,

    /// <summary>
    /// MQTT协议
    /// </summary>
    Mqtt,

    /// <summary>
    /// OPC UA协议
    /// </summary>
    OpcUa,

    /// <summary>
    /// Modbus协议
    /// </summary>
    Modbus,

    /// <summary>
    /// SECS/HSMS 协议
    /// </summary>
    Secs
}
