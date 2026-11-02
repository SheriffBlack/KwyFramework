using Secs4Net;

namespace Kwy.Communicate.Gem;

public sealed class S2F15Parser : IGemMessageParser<IReadOnlyList<GemEquipmentConstantChange>>
{
    public GemParseResult<IReadOnlyList<GemEquipmentConstantChange>> Parse(SecsMessage message)
    {
        if (!GemItemReader.TryList(message.SecsItem, null, out var pairs, out string? error))
            return GemParseResult<IReadOnlyList<GemEquipmentConstantChange>>.Failure(error!);
        var result = new List<GemEquipmentConstantChange>(pairs.Count);
        foreach (Item pair in pairs)
        {
            if (!GemItemReader.TryList(pair, 2, out var fields, out error) ||
                !GemItemReader.TryUInt32(fields[0], out uint ecid))
                return GemParseResult<IReadOnlyList<GemEquipmentConstantChange>>.Failure(error ?? "S2F15 contains an invalid ECID/value pair.");
            result.Add(new(new GemEcid(ecid), fields[1]));
        }
        return GemParseResult<IReadOnlyList<GemEquipmentConstantChange>>.Success(result);
    }
}

public sealed class S2F15Handler : IGemPrimaryMessageHandler
{
    private readonly GemRegistry registry;
    private readonly IGemEquipmentSession session;
    private readonly Func<IReadOnlyList<GemEquipmentConstantChange>, CancellationToken, Task<bool>> writer;
    private readonly S2F15Parser parser = new();

    public S2F15Handler(
        GemRegistry registry,
        IGemEquipmentSession session,
        Func<IReadOnlyList<GemEquipmentConstantChange>, CancellationToken, Task<bool>> writer)
    {
        this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    public SecsMessageId MessageId { get; } = GemMessageDefinitions.EquipmentConstantSend.Id;

    public async Task<SecsMessage?> HandleAsync(SecsMessage message, CancellationToken cancellationToken = default)
    {
        GemParseResult<IReadOnlyList<GemEquipmentConstantChange>> parsed = parser.Parse(message);
        var policy = new GemSessionPolicy(AllowedControlStates: new HashSet<GemControlState> { GemControlState.OnlineRemote });
        if (!parsed.IsSuccess || !policy.Validate(session.Context).IsValid ||
            !new GemEquipmentConstantChangeValidator(registry.Catalog).Validate(parsed.Value!).IsValid)
            return GemMessageFactory.EquipmentConstantAcknowledge(accepted: false);

        bool accepted = await writer(parsed.Value!, cancellationToken).ConfigureAwait(false);
        return GemMessageFactory.EquipmentConstantAcknowledge(accepted);
    }
}
