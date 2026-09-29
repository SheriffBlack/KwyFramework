using Kwy.Communicate.Abstractions;
using Kwy.Communicate.Core;

namespace Kwy.Communicate.Mqtt;

public static class CommunicationFactoryExtensions
{
    public static ICommunicationFactoryRegistry RegisterMqtt(this ICommunicationFactoryRegistry factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        factory.RegisterCreator<MqttConfig>(config => new MqttCommunication(config));
        return factory;
    }
}
