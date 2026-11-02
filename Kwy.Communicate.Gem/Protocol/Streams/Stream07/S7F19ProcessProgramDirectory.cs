using Secs4Net;

namespace Kwy.Communicate.Gem;

public sealed class S7F19Handler : IGemPrimaryMessageHandler
{
    private static readonly GemSessionPolicy Policy = new(
        AllowedControlStates: new HashSet<GemControlState>
        {
            GemControlState.OnlineLocal,
            GemControlState.OnlineRemote
        });
    private readonly IGemEquipmentSession session;

    public S7F19Handler(IGemEquipmentSession session)
        => this.session = session ?? throw new ArgumentNullException(nameof(session));

    public SecsMessageId MessageId { get; } = GemMessageDefinitions.CurrentProcessProgramDirectoryRequest.Id;

    public async Task<SecsMessage?> HandleAsync(SecsMessage message, CancellationToken cancellationToken = default)
    {
        if (message.SecsItem is not null || !Policy.Validate(session.Context).IsValid)
            return null;

        IReadOnlyList<string> ppids = await session.ListProcessProgramIdsAsync(cancellationToken)
            .ConfigureAwait(false);
        return GemMessageFactory.CurrentProcessProgramDirectoryData(ppids);
    }
}
