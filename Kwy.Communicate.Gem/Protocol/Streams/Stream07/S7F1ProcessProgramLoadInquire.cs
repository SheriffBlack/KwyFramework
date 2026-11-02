using Secs4Net;

namespace Kwy.Communicate.Gem;

public sealed class S7F1Parser : IGemMessageParser<GemProcessProgramLoadInquire>
{
    public GemParseResult<GemProcessProgramLoadInquire> Parse(SecsMessage message)
    {
        if (!GemItemReader.TryList(message.SecsItem, 2, out var fields, out string? error) ||
            fields[0].Format != SecsFormat.ASCII ||
            !GemItemReader.TryUInt32(fields[1], out uint length))
            return GemParseResult<GemProcessProgramLoadInquire>.Failure(
                error ?? "S7F1 must contain ASCII PPID and an unsigned LENGTH.");

        string ppid = fields[0].GetString();
        return string.IsNullOrWhiteSpace(ppid)
            ? GemParseResult<GemProcessProgramLoadInquire>.Failure("S7F1 PPID cannot be empty.")
            : GemParseResult<GemProcessProgramLoadInquire>.Success(new(ppid, length));
    }
}

public sealed class S7F1Handler : IGemPrimaryMessageHandler
{
    private static readonly GemSessionPolicy Policy = new(
        AllowedControlStates: new HashSet<GemControlState> { GemControlState.OnlineRemote });
    private readonly IGemEquipmentSession session;
    private readonly IGemProcessProgramLoadPolicy loadPolicy;
    private readonly S7F1Parser parser = new();

    public S7F1Handler(IGemEquipmentSession session, IGemProcessProgramLoadPolicy loadPolicy)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.loadPolicy = loadPolicy ?? throw new ArgumentNullException(nameof(loadPolicy));
    }

    public SecsMessageId MessageId { get; } = GemMessageDefinitions.ProcessProgramLoadInquire.Id;

    public async Task<SecsMessage?> HandleAsync(SecsMessage message, CancellationToken cancellationToken = default)
    {
        GemParseResult<GemProcessProgramLoadInquire> parsed = parser.Parse(message);
        if (!parsed.IsSuccess)
            return GemMessageFactory.ProcessProgramLoadGrant(GemProcessProgramLoadGrant.WillNotAccept);
        if (!Policy.Validate(session.Context).IsValid)
            return GemMessageFactory.ProcessProgramLoadGrant(GemProcessProgramLoadGrant.Busy);

        GemProcessProgramLoadGrant grant = await loadPolicy.EvaluateAsync(
            parsed.Value!, session.Context, cancellationToken).ConfigureAwait(false);
        return GemMessageFactory.ProcessProgramLoadGrant(grant);
    }
}
