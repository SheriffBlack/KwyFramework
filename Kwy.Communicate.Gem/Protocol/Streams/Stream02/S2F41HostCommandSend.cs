using Secs4Net;

namespace Kwy.Communicate.Gem;

public sealed class S2F41Parser : IGemMessageParser<GemRemoteCommandRequest>
{
    /*
    Host 发来：
        S2F41 W
        <L [2]
            <A "START">
            <L [2]
                <L [2]
                    <A "PPID">
                    <A "PECVD_001">
                >
                <L [2]
                    <A "LOTID">
                    <A "LOT_001">
                >
            >
        >
    Parse 解析之后得到：
        var command = new GemRemoteCommandRequest(
        "START",
        new Dictionary<string, Item>
        {
            ["PPID"] = A("PECVD_001"),
            ["LOTID"] = A("LOT_001")
        });
    */
    public GemParseResult<GemRemoteCommandRequest> Parse(SecsMessage message)
    {
        if (!GemItemReader.TryList(message.SecsItem, 2, out var fields, out string? error) ||
            fields[0].Format != SecsFormat.ASCII ||
            !GemItemReader.TryList(fields[1], null, out var pairs, out error))
            return GemParseResult<GemRemoteCommandRequest>.Failure(error ?? "S2F41 structure is invalid.");
        var parameters = new Dictionary<string, Item>(StringComparer.OrdinalIgnoreCase);
        foreach (Item pair in pairs)
        {
            if (!GemItemReader.TryList(pair, 2, out var parameter, out error) || parameter[0].Format != SecsFormat.ASCII)
                return GemParseResult<GemRemoteCommandRequest>.Failure(error ?? "S2F41 contains an invalid command parameter.");
            string name = parameter[0].GetString();
            if (!parameters.TryAdd(name, parameter[1]))
                return GemParseResult<GemRemoteCommandRequest>.Failure($"Parameter '{name}' is duplicated.");
        }
        return GemParseResult<GemRemoteCommandRequest>.Success(new(fields[0].GetString(), parameters));
    }
}

public sealed class S2F41Handler : IGemPrimaryMessageHandler
{
    private readonly IGemEquipmentSession session;
    private readonly S2F41Parser parser = new();

    public S2F41Handler(IGemEquipmentSession session)
        => this.session = session ?? throw new ArgumentNullException(nameof(session));

    public SecsMessageId MessageId { get; } = GemMessageDefinitions.HostCommandSend.Id;

    public async Task<SecsMessage?> HandleAsync(SecsMessage message, CancellationToken cancellationToken = default)
    {
        GemParseResult<GemRemoteCommandRequest> parsed = parser.Parse(message);
        if (!parsed.IsSuccess)
            return GemMessageFactory.HostCommandAcknowledge(GemHostCommandAckCode.InvalidParameter);    // 结构不正确，直接构造 S2F42，返回 InvalidParameter
        GemRemoteCommandResult result = await session.ExecuteRemoteCommandAsync(parsed.Value!, cancellationToken).ConfigureAwait(false);
        return GemMessageFactory.HostCommandAcknowledge(result.AckCode);
    }
}
