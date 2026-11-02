using Secs4Net;

namespace Kwy.Communicate.Gem;

public sealed class S7F17Parser : IGemMessageParser<IReadOnlyList<string>>
{
    public GemParseResult<IReadOnlyList<string>> Parse(SecsMessage message)
    {
        if (!GemItemReader.TryList(message.SecsItem, null, out var items, out string? error))
            return GemParseResult<IReadOnlyList<string>>.Failure(error!);
        if (items.Count == 0)
            return GemParseResult<IReadOnlyList<string>>.Failure("S7F17 must contain at least one PPID.");

        var ppids = new List<string>(items.Count);
        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Item item in items)
        {
            if (item.Format != SecsFormat.ASCII || string.IsNullOrWhiteSpace(item.GetString()))
                return GemParseResult<IReadOnlyList<string>>.Failure("S7F17 contains an invalid PPID.");
            string ppid = item.GetString();
            if (!unique.Add(ppid))
                return GemParseResult<IReadOnlyList<string>>.Failure($"S7F17 contains duplicate PPID '{ppid}'.");
            ppids.Add(ppid);
        }

        return GemParseResult<IReadOnlyList<string>>.Success(ppids);
    }
}

public sealed class S7F17Handler : IGemPrimaryMessageHandler
{
    private static readonly GemSessionPolicy Policy = new(
        AllowedControlStates: new HashSet<GemControlState> { GemControlState.OnlineRemote });
    private readonly IGemEquipmentSession session;
    private readonly S7F17Parser parser = new();

    public S7F17Handler(IGemEquipmentSession session)
        => this.session = session ?? throw new ArgumentNullException(nameof(session));

    public SecsMessageId MessageId { get; } = GemMessageDefinitions.DeleteProcessProgram.Id;

    public async Task<SecsMessage?> HandleAsync(SecsMessage message, CancellationToken cancellationToken = default)
    {
        GemParseResult<IReadOnlyList<string>> parsed = parser.Parse(message);
        if (!parsed.IsSuccess || !Policy.Validate(session.Context).IsValid)
            return GemMessageFactory.DeleteProcessProgramAcknowledge(
                GemProcessProgramAcknowledgeCode.PermissionNotGranted);

        foreach (string ppid in parsed.Value!)
        {
            using GemProcessProgram? existing = await session.FindProcessProgramAsync(
                ppid, cancellationToken).ConfigureAwait(false);
            if (existing is null)
                return GemMessageFactory.DeleteProcessProgramAcknowledge(
                    GemProcessProgramAcknowledgeCode.PpidNotFound);
        }

        foreach (string ppid in parsed.Value!)
        {
            GemProcessProgramDeleteResult result = await session.DeleteProcessProgramAsync(
                ppid, cancellationToken).ConfigureAwait(false);
            if (!result.Succeeded)
                return GemMessageFactory.DeleteProcessProgramAcknowledge(
                    GemProcessProgramAcknowledgeCode.PermissionNotGranted);
        }

        return GemMessageFactory.DeleteProcessProgramAcknowledge(GemProcessProgramAcknowledgeCode.Accepted);
    }
}
