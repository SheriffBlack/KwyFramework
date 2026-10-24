using Kwy.Communicate.Abstractions.Enums;
using Kwy.Communicate.Abstractions.Events;

namespace Kwy.Communicate.Abstractions;

/// <summary>
/// 所有协议客户端共享的连接生命周期契约。
/// </summary>
public interface IProtocolClient : IDisposable, IAsyncDisposable
{
    ConnectionState State { get; }
    bool IsConnected { get; }

    /// <summary>
    /// 异步地在线程池中的线程上按状态转换顺序执行。
    /// 观察者异常与通信状态机是相互隔离的。
    /// </summary>
    event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;

    /// <summary>
    /// 异步地按发生顺序在线程池的线程上执行。
    /// 观察者异常与通信状态机是相互隔离的。
    /// </summary>
    event EventHandler<ErrorOccurredEventArgs>? ErrorOccurred;

    /// <summary>建立连接；失败时保留错误状态并将异常返回给调用方。</summary>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 尝试建立连接。成功返回 <see langword="true"/>；
    /// 可预期的连接失败返回 <see langword="false"/>，并通过状态和错误事件报告。
    /// 取消操作仍会抛出 <see cref="OperationCanceledException"/>。
    /// </summary>
    Task<bool> TryConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>断开连接并停止与当前连接相关的后台任务。</summary>
    Task DisconnectAsync(CancellationToken cancellationToken = default);
}
