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
    private HttpClient? httpClient;

    public HttpCommunication(HttpConfig config) : base(config)
    {
        httpConfig = config ?? throw new ArgumentNullException(nameof(config));
    }

    protected override Task ConnectCoreAsync(CancellationToken cancellationToken)
    {
        var handler = new HttpClientHandler();
        if (!httpConfig.ValidateCertificate)
            handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;

        httpClient = new HttpClient(handler)
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

    public ValueTask<HttpResponseMessage> SendAsync(
        HttpMethod? method = null,
        HttpContent? content = null,
        CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(method ?? httpConfig.Method, httpConfig.Url)
        {
            Content = content
        };
        return SendAsync(request, cancellationToken);
    }
}
