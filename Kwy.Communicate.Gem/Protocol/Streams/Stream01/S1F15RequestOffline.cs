using Secs4Net;

namespace Kwy.Communicate.Gem;

/// <summary>处理 Host 发起的 S1F15 Request OFF-LINE。</summary>
public sealed class S1F15Handler : IGemPrimaryMessageHandler
{
    private readonly IGemEquipmentSession session;

    public S1F15Handler(IGemEquipmentSession session)
        => this.session = session ?? throw new ArgumentNullException(nameof(session));

    public SecsMessageId MessageId { get; } = GemMessageDefinitions.RequestOffline.Id;

    public async Task<SecsMessage?> HandleAsync(
        SecsMessage message,
        CancellationToken cancellationToken = default)
    {
        await session.AcceptHostOfflineRequestAsync(cancellationToken).ConfigureAwait(false);
        return GemMessageFactory.OfflineAcknowledge();
    }
}
