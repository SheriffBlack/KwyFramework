namespace Kwy.Communicate.Abstractions;

/// <summary>
/// Request-response communication client.
/// </summary>
public interface IRequestClient<TRequest, TResponse> : ICommunicationClient
{
    ValueTask<TResponse> SendAsync(TRequest request, CancellationToken cancellationToken = default);
}
