using Kwy.Communicate.Abstractions.Events;
using Secs4Net;
using ProtocolConnectionState = Kwy.Communicate.Abstractions.Enums.ConnectionState;

namespace Kwy.Communicate.Gem;

public sealed partial class GemEquipmentSession
{
    public async Task EstablishCommunicationAsync(CancellationToken cancellationToken = default)
    {
        registry.SealConfiguration();
        if (!secsClient.IsConnected)
            await secsClient.ConnectAsync(cancellationToken).ConfigureAwait(false);

        using SecsMessage request = GemMessageFactory.EstablishCommunicationsRequest(
            LocalEndpoint.Model ?? "KWY", LocalEndpoint.SoftwareRevision ?? "1.0");
        using SecsMessage? reply = await secsClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (reply is null || reply.S != 1 || reply.F != 14)
            throw new InvalidOperationException("Expected an S1F14 Establish Communications Acknowledge response.");
        if (!GemItemReader.TryList(reply.SecsItem, 2, out IReadOnlyList<Item> fields, out string? error) ||
            fields[0].Format != SecsFormat.Binary || fields[0].Count != 1)
        {
            throw new InvalidOperationException(
                $"S1F14 contains an invalid COMMACK structure. {error}".TrimEnd());
        }

        byte communicationAck = fields[0].FirstValue<byte>();
        if (communicationAck != 0)
            throw new InvalidOperationException($"Host rejected Establish Communications with COMMACK={communicationAck}.");
        StateMachine.MarkCommunicating();
    }

    public Task AcceptCommunicationAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        registry.SealConfiguration();
        StateMachine.MarkCommunicating("Accepted Host S1F13 Establish Communications Request");
        return Task.CompletedTask;
    }

    public async Task SetOnlineAsync(bool remote, CancellationToken cancellationToken = default)
    {
        if (CommunicationState != GemCommunicationState.Communicating)
            throw new InvalidOperationException("GEM communication must be established before going online.");
        await controlStateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            StateMachine.BeginAttemptOnline();
            using SecsMessage request = GemMessageFactory.AreYouThereRequest();
            using SecsMessage? reply = await secsClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (reply is null || reply.S != 1 || reply.F != 2)
            {
                StateMachine.RejectOnline("Host did not return S1F2 On Line Data");
                throw new InvalidOperationException("Expected an S1F2 On Line Data response before entering Online state.");
            }
            StateMachine.CompleteOnline(remote);
        }
        catch
        {
            if (ControlState == GemControlState.AttemptOnline)
                StateMachine.RejectOnline();
            throw;
        }
        finally { controlStateLock.Release(); }
    }

    public async Task AcceptHostOnlineRequestAsync(bool remote, CancellationToken cancellationToken = default)
    {
        await controlStateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            StateMachine.BeginAttemptOnline("Accepted Host S1F17 Request ON-LINE");
            StateMachine.CompleteOnline(remote, "Host S1F17 Request ON-LINE accepted");
        }
        catch
        {
            if (ControlState == GemControlState.AttemptOnline)
                StateMachine.RejectOnline("Host S1F17 Request ON-LINE failed");
            throw;
        }
        finally { controlStateLock.Release(); }
    }

    public async Task SetOfflineAsync(CancellationToken cancellationToken = default)
    {
        await controlStateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { StateMachine.SetOffline(); }
        finally { controlStateLock.Release(); }
    }

    private void OnConnectionStateChanged(object? sender, ConnectionStateChangedEventArgs args)
    {
        if (args.CurrentState != ProtocolConnectionState.Connected)
            StateMachine.MarkCommunicationLost($"Transport state changed to {args.CurrentState}");
    }
}
