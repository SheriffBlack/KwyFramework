using Kwy.Mes.Abstractions.Enums;
using Kwy.Mes.Abstractions.Events;
using Kwy.Mes.Abstractions.Models;

namespace Kwy.Mes.Abstractions;

public interface IMesConnection
{
    MesOnlineState State { get; }

    bool IsOnline { get; }

    event EventHandler<MesStateChangedEventArgs>? StateChanged;

    Task<MesResult> ConnectAsync(CancellationToken cancellationToken = default);

    Task<MesResult> DisconnectAsync(CancellationToken cancellationToken = default);
}
