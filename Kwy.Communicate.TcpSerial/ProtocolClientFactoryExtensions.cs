using Kwy.Communicate.Abstractions;
using Kwy.Communicate.Core;
using Kwy.Communicate.TcpSerial.Configs;

namespace Kwy.Communicate.TcpSerial;

public static class ProtocolClientFactoryExtensions
{
    /// <summary>仅注册 TCP 客户端创建器。</summary>
    public static ProtocolClientFactoryBuilder RegisterTcp(this ProtocolClientFactoryBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.RegisterCreator<TcpConfig>(config => new TcpCommunication(config));
    }

    /// <summary>仅注册串口客户端创建器。</summary>
    public static ProtocolClientFactoryBuilder RegisterSerialPort(this ProtocolClientFactoryBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.RegisterCreator<SerialPortConfig>(config => new SerialPortCommunication(config));
    }

    /// <summary>仅注册 HTTP 客户端创建器。</summary>
    public static ProtocolClientFactoryBuilder RegisterHttp(
        this ProtocolClientFactoryBuilder builder,
        IHttpMessageHandlerFactory? httpMessageHandlerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.RegisterCreator<HttpConfig>(config => new HttpCommunication(config, httpMessageHandlerFactory));
    }

    /// <summary>
    /// 一次性注册 TCP、串口和 HTTP 客户端创建器，便于需要完整功能的宿主使用。
    /// 如果仅使用部分功能，建议调用对应的单项注册方法。
    /// </summary>
    public static ProtocolClientFactoryBuilder RegisterTcpSerialClients(
        this ProtocolClientFactoryBuilder builder,
        IHttpMessageHandlerFactory? httpMessageHandlerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder
            .RegisterTcp()
            .RegisterSerialPort()
            .RegisterHttp(httpMessageHandlerFactory);
    }
}
