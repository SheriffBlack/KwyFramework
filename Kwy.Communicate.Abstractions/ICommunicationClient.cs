using Kwy.Communicate.Abstractions.Enums;
using Kwy.Communicate.Abstractions.Events;

namespace Kwy.Communicate.Abstractions;

/// <summary>
/// Common lifecycle contract shared by all communication clients.
/// </summary>
public interface ICommunicationClient : IDisposable, IAsyncDisposable
{
    ConnectionState State { get; }
    bool IsConnected { get; }
    /// <summary>
    /// Raised asynchronously, in state-transition order, on a thread-pool thread.
    /// Observer exceptions are isolated from the communication state machine.
    /// </summary>
    event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;

    /// <summary>
    /// Raised asynchronously, in occurrence order, on a thread-pool thread.
    /// Observer exceptions are isolated from the communication state machine.
    /// </summary>
    event EventHandler<ErrorOccurredEventArgs>? ErrorOccurred;

    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
}
