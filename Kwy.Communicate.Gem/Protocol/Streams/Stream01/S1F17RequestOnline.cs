using Secs4Net;

namespace Kwy.Communicate.Gem;

public sealed class S1F17Handler : IGemPrimaryMessageHandler
{
    private readonly IGemEquipmentSession session;
    private readonly Func<CancellationToken, Task<bool>> authorize;
    private readonly bool enterRemote;

    public S1F17Handler(
        IGemEquipmentSession session,
        Func<CancellationToken, Task<bool>> authorize,
        bool enterRemote = false)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.authorize = authorize ?? throw new ArgumentNullException(nameof(authorize));
        this.enterRemote = enterRemote;
    }

    public SecsMessageId MessageId { get; } = GemMessageDefinitions.RequestOnline.Id;

    public async Task<SecsMessage?> HandleAsync(
        SecsMessage message,
        CancellationToken cancellationToken = default)
    {
        if (session.CommunicationState != GemCommunicationState.Communicating ||
            !await authorize(cancellationToken).ConfigureAwait(false))
            return GemMessageFactory.OnlineAcknowledge(accepted: false);

        await session.AcceptHostOnlineRequestAsync(enterRemote, cancellationToken).ConfigureAwait(false);
        return GemMessageFactory.OnlineAcknowledge(accepted: true);
    }
}
