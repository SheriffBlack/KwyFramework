using System.Globalization;
using Secs4Net;

namespace Kwy.Communicate.Gem;

public sealed class S2F23Parser : IGemMessageParser<GemTraceRequest>
{
    public GemParseResult<GemTraceRequest> Parse(SecsMessage message)
    {
        if (!GemItemReader.TryList(message.SecsItem, 5, out var fields, out string? error) ||
            !GemItemReader.TryUInt32(fields[0], out uint traceId) ||
            fields[1].Format != SecsFormat.ASCII ||
            !GemItemReader.TryUInt32(fields[2], out uint totalSamples) ||
            !GemItemReader.TryList(fields[4], null, out var svidItems, out error))
            return GemParseResult<GemTraceRequest>.Failure(error ?? "S2F23 structure is invalid.");

        string dsper = fields[1].GetString();
        if (dsper.Length != 8 || !uint.TryParse(dsper, NumberStyles.None, CultureInfo.InvariantCulture, out _))
            return GemParseResult<GemTraceRequest>.Failure("DSPER must use hhmmsscc format.");
        int hours = int.Parse(dsper[..2], CultureInfo.InvariantCulture);
        int minutes = int.Parse(dsper.Substring(2, 2), CultureInfo.InvariantCulture);
        int seconds = int.Parse(dsper.Substring(4, 2), CultureInfo.InvariantCulture);
        int centiseconds = int.Parse(dsper.Substring(6, 2), CultureInfo.InvariantCulture);
        if (hours > 23 || minutes > 59 || seconds > 59)
            return GemParseResult<GemTraceRequest>.Failure("DSPER contains an invalid time value.");
        var interval = new TimeSpan(0, hours, minutes, seconds, centiseconds * 10);
        var vids = new List<GemVid>(svidItems.Count);
        foreach (Item item in svidItems)
        {
            if (!GemItemReader.TryUInt32(item, out uint vid))
                return GemParseResult<GemTraceRequest>.Failure("S2F23 contains an invalid SVID.");
            vids.Add(new GemVid(vid));
        }
        return GemParseResult<GemTraceRequest>.Success(new(traceId, interval, totalSamples, vids));
    }
}

public sealed class S2F23Handler : IGemPrimaryMessageHandler
{
    private readonly GemRegistry registry;
    private readonly IGemEquipmentSession session;
    private readonly S2F23Parser parser = new();

    public S2F23Handler(GemRegistry registry, IGemEquipmentSession session)
    {
        this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
        this.session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public SecsMessageId MessageId { get; } = GemMessageDefinitions.TraceInitializeSend.Id;

    public Task<SecsMessage?> HandleAsync(SecsMessage message, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        GemParseResult<GemTraceRequest> parsed = parser.Parse(message);
        var policy = new GemSessionPolicy(AllowedControlStates: new HashSet<GemControlState>
        {
            GemControlState.OnlineLocal,
            GemControlState.OnlineRemote
        });
        if (!parsed.IsSuccess || !policy.Validate(session.Context).IsValid ||
            !new GemTraceRequestValidator(registry.Catalog).Validate(parsed.Value!).IsValid)
            return Task.FromResult<SecsMessage?>(GemMessageFactory.TraceInitializeAcknowledge(accepted: false));

        GemTraceRequest request = parsed.Value!;
        registry.RegisterTrace(new GemTraceDefinition(
            request.TraceId,
            request.SampleInterval,
            request.TotalSamples,
            request.VariableIds));
        return Task.FromResult<SecsMessage?>(GemMessageFactory.TraceInitializeAcknowledge(accepted: true));
    }
}
