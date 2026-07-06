using HslCommunication;
using HslCommunication.Core;

namespace Kwy.Device.PLCs.Hsl;

internal sealed class HslPlcClientSession
{
    public HslPlcClientSession(
        IReadWriteNet client,
        Func<OperateResult> connect,
        Func<OperateResult> disconnect,
        string description)
    {
        Client = client ?? throw new ArgumentNullException(nameof(client));
        Connect = connect ?? throw new ArgumentNullException(nameof(connect));
        Disconnect = disconnect ?? throw new ArgumentNullException(nameof(disconnect));
        Description = string.IsNullOrWhiteSpace(description) ? client.GetType().Name : description;
    }

    public IReadWriteNet Client { get; }

    public Func<OperateResult> Connect { get; }

    public Func<OperateResult> Disconnect { get; }

    public string Description { get; }
}
