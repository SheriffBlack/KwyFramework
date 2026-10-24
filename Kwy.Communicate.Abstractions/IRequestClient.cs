namespace Kwy.Communicate.Abstractions;

/// <summary>
/// 请求—响应模式的通信客户端。
/// </summary>
public interface IRequestClient<TRequest, TResponse> : IProtocolClient
{
    ValueTask<TResponse> SendAsync(TRequest request, CancellationToken cancellationToken = default);
}
