using Secs4Net;

namespace Kwy.Communicate.Gem;

/// <summary>
/// 提供设备侧 GEM 会话状态及常用行为的默认编排实现。
/// </summary>
public sealed class GemEquipmentSession : IGemEquipmentSession
{
    private readonly ISecsGemClient secsClient;
    private readonly GemRegistry registry;
    private readonly GemTraceService traceService;
    private readonly GemSpoolingService spoolingService;
    private readonly IGemProcessProgramRepository processProgramRepository;
    private readonly SemaphoreSlim controlStateLock = new(1, 1);

    public GemEquipmentSession(
        ISecsGemClient secsClient,
        GemRegistry registry,
        GemTraceService? traceService = null,
        GemSpoolingService? spoolingService = null,
        IGemProcessProgramRepository? processProgramRepository = null,
        GemEndpoint? local = null,
        GemEndpoint? remote = null)
    {
        this.secsClient = secsClient ?? throw new ArgumentNullException(nameof(secsClient));
        this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
        this.traceService = traceService ?? new GemTraceService(registry);
        this.spoolingService = spoolingService ?? new GemSpoolingService();
        this.processProgramRepository = processProgramRepository ?? new UnsupportedGemProcessProgramRepository();
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
        registry.ValidateAndSeal();

        if (!secsClient.IsConnected)
        {
            await secsClient.ConnectAsync(cancellationToken);
        }

        using var request = GemMessageFactory.EstablishCommunicationsRequest(
            LocalEndpoint.Model ?? "KWY",
            LocalEndpoint.SoftwareRevision ?? "1.0");
        using SecsMessage? reply = await secsClient.SendAsync(request, cancellationToken);
        if (reply is null || reply.S != 1 || reply.F != 14)
        {
            throw new InvalidOperationException("Expected an S1F14 Establish Communications Acknowledge response.");
        }

        CommunicationState = GemCommunicationState.Communicating;
    }

    public async Task SetOnlineAsync(bool remote, CancellationToken cancellationToken = default)
    {
        if (CommunicationState != GemCommunicationState.Communicating)
        {
            throw new InvalidOperationException("GEM communication must be established before going online.");
        }

        await controlStateLock.WaitAsync(cancellationToken);
        try
        {
            ControlState = GemControlState.AttemptOnline;
            using var request = GemMessageFactory.AreYouThereRequest();
            using SecsMessage? reply = await secsClient.SendAsync(request, cancellationToken);
            if (reply is null || reply.S != 1 || reply.F != 2)
            {
                ControlState = GemControlState.HostOffline;
                throw new InvalidOperationException("Expected an S1F2 On Line Data response before entering Online state.");
            }

            ControlState = remote ? GemControlState.OnlineRemote : GemControlState.OnlineLocal;
        }
        catch
        {
            if (ControlState == GemControlState.AttemptOnline)
            {
                ControlState = GemControlState.HostOffline;
            }

            throw;
        }
        finally
        {
            controlStateLock.Release();
        }
    }

    public async Task SetOfflineAsync(CancellationToken cancellationToken = default)
    {
        await controlStateLock.WaitAsync(cancellationToken);
        try
        {
            ControlState = GemControlState.Offline;
        }
        finally
        {
            controlStateLock.Release();
        }
    }

    public async Task ReportAlarmAsync(GemAlarm alarm, CancellationToken cancellationToken = default)
    {
        if (!registry.Catalog.Alarms.TryGetValue(alarm.AlarmId, out GemAlarmDefinition? definition))
        {
            throw new InvalidOperationException($"ALID {alarm.AlarmId} is not defined in the GEM interface catalog.");
        }

        if (!definition.Enabled)
        {
            throw new InvalidOperationException($"ALID {alarm.AlarmId} is disabled.");
        }

        registry.SetAlarm(alarm);
        await SendPrimaryOrSpoolAsync(GemMessageFactory.AlarmReport(alarm), cancellationToken);
    }

    public async Task ReportEventAsync(uint eventId, CancellationToken cancellationToken = default)
    {
        if (!registry.Catalog.Events.TryGetValue(eventId, out GemCollectionEventDefinition? collectionEvent))
        {
            throw new KeyNotFoundException($"Collection event {eventId} is not registered.");
        }

        if (!collectionEvent.Enabled)
        {
            throw new InvalidOperationException($"Collection event {eventId} is disabled.");
        }

        GemReportDefinition[] reports = collectionEvent.LinkedReports
            .Select(id => registry.Catalog.Reports[id.Value])
            .ToArray();

        IReadOnlyDictionary<GemVid, GemVariable> eventSnapshot = registry.Data.CaptureRequired(
            reports.SelectMany(report => report.VariableIds));

        foreach ((GemVid vid, GemVariable value) in eventSnapshot)
        {
            GemVariableDefinition definition = registry.Catalog.Variables[vid.Value];
            if (definition.Format is SecsFormat expectedFormat && value.Value.Format != expectedFormat)
            {
                throw new InvalidOperationException(
                    $"VID {vid.Value} expects SECS format {expectedFormat}, but runtime value uses {value.Value.Format}.");
            }
        }

        await SendPrimaryOrSpoolAsync(
            GemMessageFactory.EventReport(collectionEvent.Ceid, reports, eventSnapshot),
            cancellationToken);
    }

    public async Task SendTerminalMessageAsync(GemTerminalMessage message, CancellationToken cancellationToken = default)
    {
        await SendPrimaryOrSpoolAsync(GemMessageFactory.TerminalMessage(message), cancellationToken);
    }

    public async Task<GemProcessProgramSaveResult> SaveProcessProgramAsync(
        GemProcessProgram processProgram,
        GemProcessProgramSaveOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(processProgram);
        GemProcessProgramSaveResult result = await processProgramRepository.SaveAsync(
            processProgram,
            options,
            cancellationToken);
        registry.RecordProcessProgramChange(
            processProgram.Ppid,
            result.Succeeded ? "Save" : "SaveRejected",
            reason: result.Message);
        return result;
    }

    public Task<GemProcessProgram?> FindProcessProgramAsync(
        string ppid,
        CancellationToken cancellationToken = default)
        => processProgramRepository.FindAsync(ppid, cancellationToken);

    public Task<IReadOnlyList<string>> ListProcessProgramIdsAsync(
        CancellationToken cancellationToken = default)
        => processProgramRepository.ListPpidsAsync(cancellationToken);

    public async Task<GemProcessProgramDeleteResult> DeleteProcessProgramAsync(
        string ppid,
        CancellationToken cancellationToken = default)
    {
        GemProcessProgramDeleteResult result = await processProgramRepository.DeleteAsync(ppid, cancellationToken);
        registry.RecordProcessProgramChange(
            ppid,
            result.Succeeded ? "Delete" : "DeleteRejected",
            reason: result.Message);
        return result;
    }

    public Task<GemTraceSample> CaptureTraceAsync(uint traceId, uint sampleNumber, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(traceService.Capture(traceId, sampleNumber));
    }

    public async Task<GemTraceRunResult> RunTraceAsync(
        uint traceId,
        CancellationToken cancellationToken = default)
    {
        if (!registry.Traces.TryGetValue(traceId, out GemTraceDefinition? trace))
        {
            throw new KeyNotFoundException($"Trace {traceId} is not registered.");
        }

        if (trace.State == GemTraceState.Disabled)
        {
            throw new InvalidOperationException($"Trace {traceId} is disabled.");
        }

        if (trace.TotalSamples == 0)
        {
            throw new InvalidOperationException($"Trace {traceId} must contain at least one sample.");
        }

        if (trace.SampleInterval < TimeSpan.Zero)
        {
            throw new InvalidOperationException($"Trace {traceId} sample interval cannot be negative.");
        }

        DateTimeOffset startedAt = DateTimeOffset.Now;
        uint reported = 0;

        try
        {
            for (uint sampleNumber = 1; sampleNumber <= trace.TotalSamples; sampleNumber++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                GemTraceSample sample = traceService.Capture(traceId, sampleNumber);
                await SendPrimaryOrSpoolAsync(GemMessageFactory.TraceDataSend(sample), cancellationToken);
                reported++;

                if (sampleNumber < trace.TotalSamples && trace.SampleInterval > TimeSpan.Zero)
                {
                    await Task.Delay(trace.SampleInterval, cancellationToken);
                }
            }

            return new GemTraceRunResult(
                traceId,
                reported,
                GemTraceState.Completed,
                startedAt,
                DateTimeOffset.Now);
        }
        catch (OperationCanceledException)
        {
            return new GemTraceRunResult(
                traceId,
                reported,
                GemTraceState.Cancelled,
                startedAt,
                DateTimeOffset.Now);
        }
    }

    public async Task<GemRemoteCommandResult> ExecuteRemoteCommandAsync(
        GemRemoteCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!registry.Catalog.RemoteCommands.TryGetValue(
                command.CommandName,
                out GemRemoteCommandDefinition? definition))
        {
            return new GemRemoteCommandResult(
                GemAckCode.Denied,
                $"Remote command {command.CommandName} is not defined.",
                GemRemoteCommandCompletionStatus.Rejected);
        }

        GemRemoteCommandValidationResult validation = GemRemoteCommandValidator.Validate(
            definition,
            command,
            ControlState);
        if (!validation.IsValid)
        {
            return new GemRemoteCommandResult(
                validation.AckCode,
                validation.Message,
                GemRemoteCommandCompletionStatus.Rejected);
        }

        if (!registry.TryGetCommand(definition.EffectiveHandlerKey, out var handler))
        {
            return new GemRemoteCommandResult(
                GemAckCode.Denied,
                $"Remote command handler {definition.EffectiveHandlerKey} is not registered.",
                GemRemoteCommandCompletionStatus.Rejected);
        }

        try
        {
            GemRemoteCommandResult result = await handler(command, cancellationToken);
            if (result.CompletionStatus != GemRemoteCommandCompletionStatus.Unspecified)
            {
                return result;
            }

            return result with
            {
                CompletionStatus = result.IsAccepted
                    ? GemRemoteCommandCompletionStatus.Completed
                    : GemRemoteCommandCompletionStatus.Rejected
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new GemRemoteCommandResult(
                GemAckCode.Busy,
                "Remote command execution was cancelled.",
                GemRemoteCommandCompletionStatus.Cancelled);
        }
        catch (Exception ex)
        {
            return new GemRemoteCommandResult(
                GemAckCode.Denied,
                ex.Message,
                GemRemoteCommandCompletionStatus.Failed);
        }
    }

    private async Task SendPrimaryOrSpoolAsync(SecsMessage message, CancellationToken cancellationToken)
    {
        try
        {
            using SecsMessage? reply = await secsClient.SendAsync(message, cancellationToken);
            message.Dispose();
        }
        catch when (spoolingService.Options.Enabled)
        {
            spoolingService.Enqueue(message);
            throw;
        }
        catch
        {
            message.Dispose();
            throw;
        }
    }
}
