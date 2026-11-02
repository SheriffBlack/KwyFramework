using Secs4Net;

namespace Kwy.Communicate.Gem;

public sealed class S7F3Parser : IGemMessageParser<GemProcessProgram>
{
    public GemParseResult<GemProcessProgram> Parse(SecsMessage message)
    {
        if (!GemItemReader.TryList(message.SecsItem, 2, out var fields, out string? error) ||
            fields[0].Format != SecsFormat.ASCII)
            return GemParseResult<GemProcessProgram>.Failure(error ?? "S7F3 structure is invalid.");
        return GemParseResult<GemProcessProgram>.Success(
            new(fields[0].GetString(), GemItemCloner.Clone(fields[1])));
    }
}

public sealed class S7F3Handler : IGemPrimaryMessageHandler
{
    private readonly IGemEquipmentSession session;
    private readonly GemProcessProgramSaveOptions saveOptions;
    private readonly S7F3Parser parser = new();
    private readonly GemProcessProgramValidator validator = new();

    public S7F3Handler(
        IGemEquipmentSession session,
        GemProcessProgramSaveOptions? saveOptions = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        this.session = session;
        this.saveOptions = saveOptions ?? new GemProcessProgramSaveOptions();
    }

    public SecsMessageId MessageId { get; } = GemMessageDefinitions.ProcessProgramSend.Id;

    public async Task<SecsMessage?> HandleAsync(SecsMessage message, CancellationToken cancellationToken = default)
    {
        GemParseResult<GemProcessProgram> parsed = parser.Parse(message);
        var policy = new GemSessionPolicy(AllowedControlStates: new HashSet<GemControlState> { GemControlState.OnlineRemote });
        if (!parsed.IsSuccess || !policy.Validate(session.Context).IsValid || !validator.Validate(parsed.Value!).IsValid)
            return GemMessageFactory.ProcessProgramAcknowledge(GemProcessProgramAcknowledgeCode.PermissionNotGranted);

        using GemProcessProgram processProgram = parsed.Value!;
        GemProcessProgramSaveResult result = await session.SaveProcessProgramAsync(
            processProgram, saveOptions, cancellationToken).ConfigureAwait(false);
        return GemMessageFactory.ProcessProgramAcknowledge(
            result.Succeeded
                ? GemProcessProgramAcknowledgeCode.Accepted
                : GemProcessProgramAcknowledgeCode.PermissionNotGranted);
    }
}
