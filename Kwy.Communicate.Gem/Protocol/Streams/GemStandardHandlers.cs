using Secs4Net;

namespace Kwy.Communicate.Gem;

public sealed class S1F3Handler : IGemPrimaryMessageHandler
{
    private readonly GemRegistry registry;
    private readonly S1F3Parser parser = new();

    public S1F3Handler(GemRegistry registry) => this.registry = registry;

    public SecsMessageId MessageId { get; } = new(1, 3);

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
        this.registry = registry;
        this.session = session;
        this.writer = writer;
    }

    public SecsMessageId MessageId { get; } = new(2, 15);

    public async Task<SecsMessage?> HandleAsync(SecsMessage message, CancellationToken cancellationToken = default)
    {
        GemParseResult<IReadOnlyList<GemEquipmentConstantChange>> parsed = parser.Parse(message);
        var policy = new GemSessionPolicy(AllowedControlStates: new HashSet<GemControlState> { GemControlState.OnlineRemote });
        if (!parsed.IsSuccess || !policy.Validate(session.Context).IsValid ||
            !new GemEquipmentConstantValidator(registry.Catalog).Validate(parsed.Value!).IsValid)
            return GemMessageFactory.EquipmentConstantAcknowledge(1);

        // writer 负责写入真实设备，并用自己拥有的 Item 更新 DataSnapshot。
        // 不把接收报文中的 Item 长期保存，避免报文释放后引用失效。
        bool accepted = await writer(parsed.Value!, cancellationToken).ConfigureAwait(false);
        return GemMessageFactory.EquipmentConstantAcknowledge(accepted ? (byte)0 : (byte)1);
    }
}

public sealed class S2F23Handler : IGemPrimaryMessageHandler
{
    private readonly GemRegistry registry;
    private readonly IGemEquipmentSession session;
    private readonly S2F23Parser parser = new();

    public S2F23Handler(GemRegistry registry, IGemEquipmentSession session)
    {
        this.registry = registry;
        this.session = session;
    }

    public SecsMessageId MessageId { get; } = new(2, 23);

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
            return Task.FromResult<SecsMessage?>(GemMessageFactory.TraceInitializeAcknowledge(1));

        GemTraceRequest request = parsed.Value!;
        registry.RegisterTrace(new GemTraceDefinition(
            request.TraceId,
            request.SampleInterval,
            request.TotalSamples,
            request.VariableIds));
        return Task.FromResult<SecsMessage?>(GemMessageFactory.TraceInitializeAcknowledge(0));
    }
}

public sealed class S2F41Handler : IGemPrimaryMessageHandler
{
    private readonly IGemEquipmentSession session;
    private readonly S2F41Parser parser = new();

    public S2F41Handler(IGemEquipmentSession session) => this.session = session;

    public SecsMessageId MessageId { get; } = new(2, 41);

    public async Task<SecsMessage?> HandleAsync(SecsMessage message, CancellationToken cancellationToken = default)
    {
        GemParseResult<GemRemoteCommand> parsed = parser.Parse(message);
        if (!parsed.IsSuccess)
            return GemMessageFactory.HostCommandAcknowledge(GemAckCode.InvalidParameter);
        GemRemoteCommandResult result = await session.ExecuteRemoteCommandAsync(parsed.Value!, cancellationToken).ConfigureAwait(false);
        return GemMessageFactory.HostCommandAcknowledge(result.AckCode);
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
        this.registry = registry;
        this.session = session;
        this.apply = apply;
    }

    public SecsMessageId MessageId { get; } = new(5, 3);

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
            return GemMessageFactory.EnableDisableAlarmAcknowledge(1);
        bool accepted = await apply(parsed.Value!, cancellationToken).ConfigureAwait(false);
        return GemMessageFactory.EnableDisableAlarmAcknowledge(accepted ? (byte)0 : (byte)1);
    }
}

public sealed class S7F3Handler : IGemPrimaryMessageHandler
{
    private readonly IGemEquipmentSession session;
    private readonly S7F3Parser parser = new();
    private readonly GemProcessProgramValidator validator = new();

    public S7F3Handler(IGemEquipmentSession session) => this.session = session;

    public SecsMessageId MessageId { get; } = new(7, 3);

    public async Task<SecsMessage?> HandleAsync(SecsMessage message, CancellationToken cancellationToken = default)
    {
        GemParseResult<GemProcessProgram> parsed = parser.Parse(message);
        var policy = new GemSessionPolicy(AllowedControlStates: new HashSet<GemControlState> { GemControlState.OnlineRemote });
        if (!parsed.IsSuccess || !policy.Validate(session.Context).IsValid || !validator.Validate(parsed.Value!).IsValid)
            return GemMessageFactory.ProcessProgramAcknowledge(1);
        GemProcessProgramSaveResult result = await session.SaveProcessProgramAsync(
            parsed.Value!,
            new GemProcessProgramSaveOptions(Overwrite: true),
            cancellationToken).ConfigureAwait(false);
        return GemMessageFactory.ProcessProgramAcknowledge(result.Succeeded ? (byte)0 : (byte)1);
    }
}
