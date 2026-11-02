using Secs4Net;

namespace Kwy.Communicate.Gem;

public sealed record GemAlarmEnableRequest(bool Enabled, GemAlid Alid);

public sealed class S5F3Parser : IGemMessageParser<GemAlarmEnableRequest>
{
    public GemParseResult<GemAlarmEnableRequest> Parse(SecsMessage message)
    {
        if (!GemItemReader.TryList(message.SecsItem, 2, out var fields, out string? error) ||
            fields[0].Format != SecsFormat.Binary || fields[0].Count != 1 ||
            !GemItemReader.TryUInt32(fields[1], out uint alid))
            return GemParseResult<GemAlarmEnableRequest>.Failure(error ?? "S5F3 structure is invalid.");
        byte aled = fields[0].FirstValue<byte>();
        if (aled is not 0 and not 0x80)
            return GemParseResult<GemAlarmEnableRequest>.Failure("ALED must be 0 or 128.");
        return GemParseResult<GemAlarmEnableRequest>.Success(new(aled == 0x80, new GemAlid(alid)));
    }
}

public sealed class S5F3Handler : IGemPrimaryMessageHandler
{
    private readonly GemRegistry registry;
    private readonly IGemEquipmentSession session;
    private readonly Func<GemAlarmEnableRequest, CancellationToken, Task<bool>> apply;
    private readonly S5F3Parser parser = new();

    public S5F3Handler(
        GemRegistry registry,
        IGemEquipmentSession session,
        Func<GemAlarmEnableRequest, CancellationToken, Task<bool>> apply)
    {
        this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.apply = apply ?? throw new ArgumentNullException(nameof(apply));
    }

    public SecsMessageId MessageId { get; } = GemMessageDefinitions.EnableDisableAlarmSend.Id;

    public async Task<SecsMessage?> HandleAsync(SecsMessage message, CancellationToken cancellationToken = default)
    {
        GemParseResult<GemAlarmEnableRequest> parsed = parser.Parse(message);
        var policy = new GemSessionPolicy(AllowedControlStates: new HashSet<GemControlState>
        {
            GemControlState.OnlineLocal,
            GemControlState.OnlineRemote
        });
        if (!parsed.IsSuccess || !policy.Validate(session.Context).IsValid ||
            !registry.Catalog.Alarms.ContainsKey(parsed.Value!.Alid.Value))
            return GemMessageFactory.EnableDisableAlarmAcknowledge(accepted: false);
        bool accepted = await apply(parsed.Value!, cancellationToken).ConfigureAwait(false);
        return GemMessageFactory.EnableDisableAlarmAcknowledge(accepted);
    }
}
