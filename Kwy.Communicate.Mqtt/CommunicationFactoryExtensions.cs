using Kwy.Communicate.Abstractions;

namespace Kwy.Communicate.Mqtt;

public static class CommunicationFactoryExtensions
{
    public static ICommunicationFactory RegisterMqtt(this ICommunicationFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        factory.RegisterCreator<MqttConfig>(config => new MqttCommunication(config));
        return factory;
    }
}
