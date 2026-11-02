using Secs4Net;

namespace Kwy.Communicate.Gem;

public sealed class S1F13Handler : IGemPrimaryMessageHandler
{
    private readonly IGemEquipmentSession session;

    public S1F13Handler(IGemEquipmentSession session)
        => this.session = session ?? throw new ArgumentNullException(nameof(session));

    public SecsMessageId MessageId { get; } = GemMessageDefinitions.EstablishCommunicationsRequest.Id;

    public async Task<SecsMessage?> HandleAsync(
        SecsMessage message,
        CancellationToken cancellationToken = default)
    {
        await session.AcceptCommunicationAsync(cancellationToken).ConfigureAwait(false);
        GemEndpoint local = session.Context.Local;
        return GemMessageFactory.EstablishCommunicationsAcknowledge(
            true,
            local.Model ?? "KWY",
            local.SoftwareRevision ?? "1.0");
    }
}
