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
    IProtocolConfig Config { get; }

    event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;
    event EventHandler<ErrorOccurredEventArgs>? ErrorOccurred;

    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
}
