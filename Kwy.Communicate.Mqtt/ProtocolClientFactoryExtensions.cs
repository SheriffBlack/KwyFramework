using Kwy.Communicate.Abstractions;
using Kwy.Communicate.Core;

namespace Kwy.Communicate.Mqtt;

/// <summary>向可选通信工厂注册 MQTT 客户端创建逻辑。</summary>
public static class ProtocolClientFactoryExtensions
{
    /// <summary>注册 MQTT 配置到客户端的映射。</summary>
    public static ProtocolClientFactoryBuilder RegisterMqtt(this ProtocolClientFactoryBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.RegisterCreator<MqttConfig>(config => new MqttCommunication(config));
    }
}
