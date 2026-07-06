using Kwy.Communicate.Secs;

namespace Kwy.Communicate.Gem;

public sealed class GemEquipmentService : IGemEquipment
{
    private readonly ISecsClient secsClient;
    private readonly GemRegistry registry;
    private readonly GemTraceService traceService;
    private readonly GemSpoolingService spoolingService;

    public GemEquipmentService(
        ISecsClient secsClient,
        GemRegistry registry,
        GemTraceService? traceService = null,
        GemSpoolingService? spoolingService = null,
        GemEndpoint? local = null,
        GemEndpoint? remote = null)
    {
        this.secsClient = secsClient ?? throw new ArgumentNullException(nameof(secsClient));
        this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
        this.traceService = traceService ?? new GemTraceService(registry);
        this.spoolingService = spoolingService ?? new GemSpoolingService();
        LocalEndpoint = local ?? new GemEndpoint(GemHostRole.Equipment, "KwyEquipment", "KWY", "1.0");
        RemoteEndpoint = remote ?? new GemEndpoint(GemHostRole.Host, "Host");
    }

    public GemEndpoint LocalEndpoint { get; }

    public GemEndpoint RemoteEndpoint { get; }

    public GemCommunicationState CommunicationState { get; private set; } = GemCommunicationState.NotCommunicating;

    public GemControlState ControlState { get; private set; } = GemControlState.Offline;

    public GemCommunicationContext Context => new(LocalEndpoint, RemoteEndpoint, CommunicationState, ControlState);

    public async Task EstablishCommunicationAsync(CancellationToken cancellationToken = default)
    {
        if (!secsClient.IsConnected)
        {
            await secsClient.ConnectAsync(cancellationToken);
        }

        await secsClient.SendPrimaryAsync(
            SecsMessageFactory.EstablishCommunicationRequest(LocalEndpoint.Model ?? "KWY", LocalEndpoint.SoftwareRevision ?? "1.0"),
            cancellationToken);

        CommunicationState = GemCommunicationState.Communicating;
    }

    public Task SetOnlineAsync(bool remote, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ControlState = remote ? GemControlState.OnlineRemote : GemControlState.OnlineLocal;
        return Task.CompletedTask;
    }

    public Task SetOfflineAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ControlState = GemControlState.Offline;
        return Task.CompletedTask;
    }

    public async Task ReportAlarmAsync(GemAlarm alarm, CancellationToken cancellationToken = default)
    {
        registry.SetAlarm(alarm);
        await SendPrimaryOrSpoolAsync(GemMessageFactory.AlarmReport(alarm), cancellationToken);
    }

    public async Task ReportEventAsync(uint eventId, CancellationToken cancellationToken = default)
    {
        if (!registry.Events.TryGetValue(eventId, out var collectionEvent))
        {
            throw new KeyNotFoundException($"Collection event {eventId} is not registered.");
        }

        var reports = collectionEvent.LinkedReportIds
            .Where(registry.Reports.ContainsKey)
            .Select(id => registry.Reports[id])
            .ToArray();

        await SendPrimaryOrSpoolAsync(GemMessageFactory.EventReport(eventId, reports, registry), cancellationToken);
    }

    public async Task SendTerminalMessageAsync(GemTerminalMessage message, CancellationToken cancellationToken = default)
    {
        await SendPrimaryOrSpoolAsync(GemMessageFactory.TerminalMessage(message), cancellationToken);
    }

    public Task SaveRecipeAsync(GemRecipe recipe, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        registry.SaveRecipe(recipe);
        return Task.CompletedTask;
    }

    public Task<GemTraceSample> CaptureTraceAsync(uint traceId, uint sampleNumber, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(traceService.Capture(traceId, sampleNumber));
    }

    public async Task<GemRemoteCommandResult> ExecuteRemoteCommandAsync(
        GemRemoteCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!registry.TryGetCommand(command.CommandName, out var handler))
        {
            return new GemRemoteCommandResult(GemAckCode.Denied, $"Remote command {command.CommandName} is not registered.");
        }

        return await handler(command, cancellationToken);
    }

    private async Task SendPrimaryOrSpoolAsync(SecsMessage message, CancellationToken cancellationToken)
    {
        try
        {
            await secsClient.SendPrimaryAsync(message, cancellationToken);
        }
        catch when (spoolingService.Options.Enabled)
        {
            spoolingService.Enqueue(message);
            throw;
        }
    }
}
