using KwyTemplate.MES.Abstract.Events;
using KwyTemplate.MES.Abstract.Models;

namespace KwyTemplate.MES.Abstract.Services;

public interface IMesConnection
{
    MesConnectionState State { get; }

    event EventHandler<MesStateChangedEventArgs>? StateChanged;

    Task<MesResult> ConnectAsync(CancellationToken cancellationToken = default);

    Task<MesResult> DisconnectAsync(CancellationToken cancellationToken = default);
}