using System.Text.Json.Serialization;

namespace Kwy.Device.PLC.Hsl.Configuration;

/// <summary>
/// HSL PLC 驱动的物理连接配置。
/// <para>HSL 同时支持 TCP 与串口；该模型仅属于 HSL 适配器，不进入跨厂商 PLC 公共抽象。</para>
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(HslTcpConnectionConfig), "tcp")]
[JsonDerivedType(typeof(HslSerialConnectionConfig), "serial")]
public abstract class HslConnectionConfig
{
    /// <summary>用于日志、诊断与连接错误提示的连接目标描述。</summary>
    public abstract string Endpoint { get; }

    /// <summary>校验连接参数。</summary>
    public abstract bool Validate();
}
