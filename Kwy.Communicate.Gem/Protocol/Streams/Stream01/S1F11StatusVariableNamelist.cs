using Secs4Net;

namespace Kwy.Communicate.Gem;

/// <summary>S1F12 中一条 SVID、SVNAME 和 UNITS 元数据。</summary>
public sealed record GemStatusVariableNameEntry(GemVid Svid, string Name, string? Unit = null);

public sealed class S1F11Parser : IGemMessageParser<IReadOnlyList<GemVid>>
{
    public GemParseResult<IReadOnlyList<GemVid>> Parse(SecsMessage message)
    {
        if (!GemItemReader.TryList(message.SecsItem, null, out IReadOnlyList<Item> items, out string? error))
            return GemParseResult<IReadOnlyList<GemVid>>.Failure(error!);

        var result = new List<GemVid>(items.Count);
        foreach (Item item in items)
        {
            if (!GemItemReader.TryUInt32(item, out uint id))
                return GemParseResult<IReadOnlyList<GemVid>>.Failure("S1F11 contains an invalid SVID.");

            result.Add(new GemVid(id));
        }

        return GemParseResult<IReadOnlyList<GemVid>>.Success(result);
    }
}

public sealed class S1F11Handler : IGemPrimaryMessageHandler
{
    private readonly GemRegistry registry;
    private readonly IGemEquipmentSession session;
    private readonly S1F11Parser parser = new();

    public S1F11Handler(GemRegistry registry, IGemEquipmentSession session)
    {
        this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
        this.session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public SecsMessageId MessageId { get; } = GemMessageDefinitions.StatusVariableNamelistRequest.Id;

    public Task<SecsMessage?> HandleAsync(SecsMessage message, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        GemParseResult<IReadOnlyList<GemVid>> parsed = parser.Parse(message);
        if (!parsed.IsSuccess || !new GemSessionPolicy().Validate(session.Context).IsValid)
            return Task.FromResult<SecsMessage?>(null);

        IReadOnlyList<GemVid> requested = parsed.Value!;
        IEnumerable<GemStatusVariableNameEntry> entries = requested.Count == 0
            ? registry.Catalog.Variables.Values
                .Where(definition => definition.Kind == GemVariableKind.StatusVariable)
                .OrderBy(definition => definition.Vid.Value)
                .Select(ToEntry)
            : requested.Select(FindEntry);

        return Task.FromResult<SecsMessage?>(GemMessageFactory.StatusVariableNamelistReply(entries));
    }

    private GemStatusVariableNameEntry FindEntry(GemVid svid)
    {
        return registry.Catalog.Variables.TryGetValue(svid.Value, out GemVariableDefinition? definition) &&
               definition.Kind == GemVariableKind.StatusVariable
            ? ToEntry(definition)
            : new GemStatusVariableNameEntry(svid, string.Empty, string.Empty);
    }

    private static GemStatusVariableNameEntry ToEntry(GemVariableDefinition definition)
        => new(definition.Vid, definition.Name, definition.Unit);
}
