using Secs4Net;

namespace Kwy.Communicate.Gem;

public sealed class S1F3Parser : IGemMessageParser<IReadOnlyList<GemVid>>
{
    public GemParseResult<IReadOnlyList<GemVid>> Parse(SecsMessage message)
    {
        if (!GemItemReader.TryList(message.SecsItem, null, out var items, out string? error))
            return GemParseResult<IReadOnlyList<GemVid>>.Failure(error!);
        var result = new List<GemVid>(items.Count);
        foreach (Item item in items)
        {
            if (!GemItemReader.TryUInt32(item, out uint id))
                return GemParseResult<IReadOnlyList<GemVid>>.Failure("S1F3 contains an invalid SVID.");
            result.Add(new GemVid(id));
        }
        return GemParseResult<IReadOnlyList<GemVid>>.Success(result);
    }
}

public sealed class S1F3Handler : IGemPrimaryMessageHandler
{
    private readonly GemRegistry registry;
    private readonly S1F3Parser parser = new();

    public S1F3Handler(GemRegistry registry)
        => this.registry = registry ?? throw new ArgumentNullException(nameof(registry));

    public SecsMessageId MessageId { get; } = GemMessageDefinitions.SelectedEquipmentStatusRequest.Id;

    public Task<SecsMessage?> HandleAsync(SecsMessage message, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        GemParseResult<IReadOnlyList<GemVid>> parsed = parser.Parse(message);
        if (!parsed.IsSuccess)
            return Task.FromResult<SecsMessage?>(null);

        IReadOnlyList<GemVid> requested = parsed.Value!;
        IEnumerable<GemVariable> variables = requested.Count == 0
            ? registry.Catalog.Variables.Keys.OrderBy(id => id).Select(id => registry.Data.GetRequiredVariable(new GemVid(id)))
            : requested.Select(registry.Data.GetRequiredVariable);
        return Task.FromResult<SecsMessage?>(GemMessageFactory.SelectedEquipmentStatusData(variables));
    }
}
