using Kwy.Communicate.TcpSerial.Configs;

namespace Kwy.Communicate.TcpSerial;

/// <summary>创建 HTTP 客户端使用的传输处理器。</summary>
public interface IHttpMessageHandlerFactory
{
    HttpMessageHandler CreateHandler(HttpConfig config);
}

/// <summary>在不使用依赖注入宿主时提供的默认独立处理器工厂。</summary>
public sealed class DefaultHttpMessageHandlerFactory : IHttpMessageHandlerFactory
{
    public HttpMessageHandler CreateHandler(HttpConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var handler = new HttpClientHandler();
        if (!config.ValidateCertificate)
        {
            // 仅允许在隔离的测试或工业网络中显式关闭证书验证，不得对不可信端点使用此配置。
            handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
        }

        return handler;
    }
}
