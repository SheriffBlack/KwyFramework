using System.Text.Json.Serialization;

namespace Kwy.Device.PLC.Abstractions;

/// <summary>
/// PLC 的物理连接配置。
/// <para>这是可多态序列化的最小连接描述；不包含 PLC 心跳、协议重连等设备运行策略。</para>
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(TcpPlcConnectionConfig), "tcp")]
[JsonDerivedType(typeof(SerialPlcConnectionConfig), "serial")]
public abstract class PlcConnectionConfig
{
    /// <summary>用于日志、诊断与连接错误提示的连接目标描述。</summary>
    public abstract string Endpoint { get; }

    /// <summary>校验连接参数。</summary>
    public abstract bool Validate();
}
