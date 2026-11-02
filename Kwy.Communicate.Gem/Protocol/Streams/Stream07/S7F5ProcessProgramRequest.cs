using Secs4Net;

namespace Kwy.Communicate.Gem;

public sealed class S7F5Parser : IGemMessageParser<string>
{
    public GemParseResult<string> Parse(SecsMessage message)
    {
        if (message.SecsItem is not { Format: SecsFormat.ASCII } item)
            return GemParseResult<string>.Failure("S7F5 must contain an ASCII PPID.");
        string ppid = item.GetString();
        return string.IsNullOrWhiteSpace(ppid)
            ? GemParseResult<string>.Failure("S7F5 PPID cannot be empty.")
            : GemParseResult<string>.Success(ppid);
    }
}

public sealed class S7F5Handler : IGemPrimaryMessageHandler
{
    private static readonly GemSessionPolicy Policy = new(
        AllowedControlStates: new HashSet<GemControlState>
        {
            GemControlState.OnlineLocal,
            GemControlState.OnlineRemote
        });
    private readonly IGemEquipmentSession session;
    private readonly S7F5Parser parser = new();

    public S7F5Handler(IGemEquipmentSession session)
        => this.session = session ?? throw new ArgumentNullException(nameof(session));

    public SecsMessageId MessageId { get; } = GemMessageDefinitions.ProcessProgramRequest.Id;

    public async Task<SecsMessage?> HandleAsync(SecsMessage message, CancellationToken cancellationToken = default)
    {
        GemParseResult<string> parsed = parser.Parse(message);
        if (!parsed.IsSuccess || !Policy.Validate(session.Context).IsValid)
            return null;

        using GemProcessProgram? processProgram = await session.FindProcessProgramAsync(
            parsed.Value!, cancellationToken).ConfigureAwait(false);
        return GemMessageFactory.ProcessProgramData(processProgram);
    }
}
