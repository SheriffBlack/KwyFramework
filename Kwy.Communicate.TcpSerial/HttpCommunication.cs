using Kwy.Communicate.Abstractions;
using Kwy.Communicate.Core;
using Kwy.Communicate.TcpSerial.Configs;

namespace Kwy.Communicate.TcpSerial;

/// <summary>
/// HTTP/HTTPS request-response client.
/// </summary>
public sealed class HttpCommunication : CommunicationClientBase, IRequestClient<HttpRequestMessage, HttpResponseMessage>
{
    private readonly HttpConfig httpConfig;
    private readonly IHttpMessageHandlerFactory handlerFactory;
    private HttpClient? httpClient;

    public HttpCommunication(HttpConfig config, IHttpMessageHandlerFactory? handlerFactory = null) : base(CloneConfig(config))
    {
        httpConfig = (HttpConfig)this.config;
        this.handlerFactory = handlerFactory ?? new DefaultHttpMessageHandlerFactory();
    }

    protected override Task ConnectCoreAsync(CancellationToken cancellationToken)
    {
        HttpMessageHandler handler = handlerFactory.CreateHandler(httpConfig)
            ?? throw new InvalidOperationException("The HTTP message-handler factory returned null.");
        httpClient = new HttpClient(handler, disposeHandler: true)
        {
            Timeout = TimeSpan.FromMilliseconds(httpConfig.Timeout)
        };

        foreach (var header in httpConfig.Headers)
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);

        return Task.CompletedTask;
    }

    protected override Task DisconnectCoreAsync(CancellationToken cancellationToken)
    {
        httpClient?.Dispose();
        httpClient = null;
        return Task.CompletedTask;
    }

    protected override bool IsConnectionAlive() => httpClient != null;

    /// <summary>
    /// Creates a request from the configured URL and method. The caller owns the returned request.
    /// </summary>
    public HttpRequestMessage CreateRequest(HttpContent? content = null)
        => new(httpConfig.Method, httpConfig.Url) { Content = content };

    public async ValueTask<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        if (!IsConnected || httpClient == null)
            throw new InvalidOperationException("HTTP client is not ready.");

        try
        {
            return await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            OnErrorOccurred(ex, $"HTTP request failed: {ex.Message}");
            throw;
        }
    }

    private static HttpConfig CloneConfig(HttpConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return new HttpConfig
        {
            Url = config.Url,
            Method = config.Method,
            Headers = new Dictionary<string, string>(config.Headers, StringComparer.OrdinalIgnoreCase),
            Timeout = config.Timeout,
            ValidateCertificate = config.ValidateCertificate,
            AutoReconnect = config.AutoReconnect,
            MaxReconnectAttempts = config.MaxReconnectAttempts,
            ReconnectInterval = config.ReconnectInterval
        };
    }
}
