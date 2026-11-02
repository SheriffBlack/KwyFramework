using Secs4Net;

namespace Kwy.Communicate.Gem;

public sealed partial class GemEquipmentSession
{
    public Task<GemSpoolReplayResult> ReplaySpooledMessagesAsync(
        int maximumMessages = int.MaxValue,
        CancellationToken cancellationToken = default)
    {
        if (CommunicationState != GemCommunicationState.Communicating || !secsClient.IsConnected)
            throw new InvalidOperationException("GEM communication must be restored before replaying the spool.");

        return spoolingService.ReplayAsync(
            async (message, token) =>
            {
                using SecsMessage? reply = await secsClient.SendAsync(message, token).ConfigureAwait(false);
            },
            maximumMessages,
            cancellationToken);
    }

    public async Task<GemSpoolReplayResult> RecoverCommunicationAsync(
        int maximumMessages = int.MaxValue,
        CancellationToken cancellationToken = default)
    {
        await EstablishCommunicationAsync(cancellationToken).ConfigureAwait(false);
        return await ReplaySpooledMessagesAsync(maximumMessages, cancellationToken).ConfigureAwait(false);
    }

    private async Task SendPrimaryOrSpoolAsync(SecsMessage message, CancellationToken cancellationToken)
    {
        if (!secsClient.IsConnected)
        {
            bool queued = spoolingService.TryEnqueue(message);
            message.Dispose();
            if (queued)
                return;
            throw new InvalidOperationException(
                "The SECS/HSMS session is not selected and the message is not eligible for spooling.");
        }

        try
        {
            using SecsMessage? reply = await secsClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            message.Dispose();
        }
        catch
        {
            bool queued = spoolingService.TryEnqueue(message, ambiguousSendFailure: true);
            message.Dispose();
            if (queued)
                return;
            throw;
        }
    }
}
