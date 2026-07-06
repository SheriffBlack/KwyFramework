using Kwy.Communicate.Abstractions;
using Kwy.Communicate.TcpSerial.Configs;

namespace Kwy.Communicate.TcpSerial;

public static class CommunicationFactoryExtensions
{
    public static ICommunicationFactory RegisterTcpSerialClients(this ICommunicationFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        factory.RegisterCreator<TcpConfig>(config => new TcpCommunication(config));
        factory.RegisterCreator<SerialPortConfig>(config => new SerialPortCommunication(config));
        factory.RegisterCreator<HttpConfig>(config => new HttpCommunication(config));
        return factory;
    }
}
