using Kwy.Communicate.Abstractions;
using Kwy.Communicate.Core;
using Kwy.Communicate.TcpSerial.Configs;

namespace Kwy.Communicate.TcpSerial;

public static class CommunicationFactoryExtensions
{
    /// <summary>Registers only the TCP client creator.</summary>
    public static CommunicationFactoryBuilder RegisterTcp(this CommunicationFactoryBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.RegisterCreator<TcpConfig>(config => new TcpCommunication(config));
    }

    /// <summary>Registers only the serial-port client creator.</summary>
    public static CommunicationFactoryBuilder RegisterSerialPort(this CommunicationFactoryBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.RegisterCreator<SerialPortConfig>(config => new SerialPortCommunication(config));
    }

    /// <summary>Registers only the HTTP client creator.</summary>
    public static CommunicationFactoryBuilder RegisterHttp(
        this CommunicationFactoryBuilder builder,
        IHttpMessageHandlerFactory? httpMessageHandlerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.RegisterCreator<HttpConfig>(config => new HttpCommunication(config, httpMessageHandlerFactory));
    }

    /// <summary>
    /// Registers TCP, serial-port, and HTTP creators as a convenience for hosts that need the whole package.
    /// Prefer the individual registration methods when only part of the package is used.
    /// </summary>
    public static CommunicationFactoryBuilder RegisterTcpSerialClients(
        this CommunicationFactoryBuilder builder,
        IHttpMessageHandlerFactory? httpMessageHandlerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder
            .RegisterTcp()
            .RegisterSerialPort()
            .RegisterHttp(httpMessageHandlerFactory);
    }
}
