using Kwy.Communicate.Abstractions;
using Kwy.Communicate.Core;
using Kwy.Communicate.TcpSerial.Configs;

namespace Kwy.Communicate.TcpSerial;

public static class CommunicationFactoryExtensions
{
    public static ICommunicationFactoryRegistry RegisterTcpSerialClients(
        this ICommunicationFactoryRegistry factory,
        IHttpMessageHandlerFactory? httpMessageHandlerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(factory);
        factory.RegisterCreator<TcpConfig>(config => new TcpCommunication(config));
        factory.RegisterCreator<SerialPortConfig>(config => new SerialPortCommunication(config));
        factory.RegisterCreator<HttpConfig>(config => new HttpCommunication(config, httpMessageHandlerFactory));
        return factory;
    }
}
