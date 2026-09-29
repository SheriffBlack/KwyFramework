using Kwy.Communicate.TcpSerial.Configs;

namespace Kwy.Communicate.TcpSerial;

/// <summary>Creates the HTTP transport handler used by an HTTP communication client.</summary>
public interface IHttpMessageHandlerFactory
{
    HttpMessageHandler CreateHandler(HttpConfig config);
}

/// <summary>Default standalone handler factory used outside dependency-injection hosts.</summary>
public sealed class DefaultHttpMessageHandlerFactory : IHttpMessageHandlerFactory
{
    public HttpMessageHandler CreateHandler(HttpConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var handler = new HttpClientHandler();
        if (!config.ValidateCertificate)
        {
            handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
        }

        return handler;
    }
}
