using KwyTemplate.MES.Events;
using KwyTemplate.MES.Models;

namespace KwyTemplate.MES.Services;

public interface IMesConnection
{
    MesConnectionState State { get; }

    event EventHandler<MesStateChangedEventArgs>? StateChanged;

    Task<MesResult> ConnectAsync(CancellationToken cancellationToken = default);

    Task<MesResult> DisconnectAsync(CancellationToken cancellationToken = default);
}