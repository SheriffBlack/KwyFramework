using Kwy.Communicate.Gem;
using Kwy.Communicate.Secs;
using Kwy.Device.Abstractions.Equipment;
using System.Collections.Concurrent;

namespace Kwy.Device.Semiconductor.Gem;

public sealed class GemEquipmentBridge : IGemEquipmentBridge
{
    private readonly IGemEquipment gemEquipment;
    private readonly GemRegistry registry;
    private readonly IEquipmentGemMapper mapper;
    private readonly GemEquipmentBridgeOptions options;
    private readonly IEquipmentStateMachine? stateMachine;
    private readonly ConcurrentQueue<EquipmentEvent> publishedEvents = new();
    private bool disposed;

    public GemEquipmentBridge(
        IGemEquipment gemEquipment,
        GemRegistry registry,
        IEquipmentGemMapper mapper,
        GemEquipmentBridgeOptions options,
        IEquipmentStateMachine? stateMachine = null)
    {
        this.gemEquipment = gemEquipment ?? throw new ArgumentNullException(nameof(gemEquipment));
        this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
        this.mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.stateMachine = stateMachine;

        if (this.stateMachine is not null && options.ReportStateChanges)
        {
            this.stateMachine.StateChanged += OnStateChanged;
        }
    }

    public IReadOnlyCollection<EquipmentEvent> PublishedEvents => publishedEvents.ToArray();

    public async Task PublishAsync(EquipmentEvent equipmentEvent, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(equipmentEvent);
        cancellationToken.ThrowIfCancellationRequested();

        var timestamped = equipmentEvent with { Timestamp = equipmentEvent.Timestamp ?? DateTimeOffset.Now };
        publishedEvents.Enqueue(timestamped);

        if (timestamped.Kind == EquipmentEventKind.Alarm && options.ReportAlarms)
        {
            await ReportAlarmAsync(timestamped, cancellationToken);
        }

        if (options.ReportEquipmentEvents)
        {
            var ceid = mapper.GetEquipmentEventId(timestamped);
            EnsureEventReportRegistered(ceid, timestamped.Code, options.EquipmentEventRptid, GetEquipmentEventVariables(timestamped));
            await gemEquipment.ReportEventAsync(ceid, cancellationToken);
        }
    }

    public async Task ReportStateChangedAsync(
        EquipmentStateChangedEventArgs args,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        if (!options.ReportStateChanges)
        {
            return;
        }

        var ceid = mapper.GetStateChangedEventId(args);
        EnsureEventReportRegistered(ceid, "EquipmentStateChanged", options.StateChangedRptid, GetStateVariables(args));
        await gemEquipment.ReportEventAsync(ceid, cancellationToken);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (stateMachine is not null)
        {
            stateMachine.StateChanged -= OnStateChanged;
        }
    }

    private async Task ReportAlarmAsync(EquipmentEvent equipmentEvent, CancellationToken cancellationToken)
    {
        var alarm = mapper.ToGemAlarm(equipmentEvent);
        registry.RegisterAlarmDefinition(new GemAlarmDefinition(
            new GemAlid(alarm.AlarmId),
            equipmentEvent.Code,
            alarm.Text,
            alarm.AlarmCode));

        await gemEquipment.ReportAlarmAsync(alarm, cancellationToken);
    }

    private IReadOnlyList<GemVariable> GetStateVariables(EquipmentStateChangedEventArgs args)
        => new[]
        {
            CreateVariable(options.StateVid, "EquipmentState", SecsItem.A(args.CurrentState.ToString())),
            CreateVariable(options.PreviousStateVid, "PreviousEquipmentState", SecsItem.A(args.PreviousState.ToString())),
            CreateVariable(options.StateReasonVid, "EquipmentStateReason", SecsItem.A(args.Reason ?? string.Empty))
        };

    private IReadOnlyList<GemVariable> GetEquipmentEventVariables(EquipmentEvent equipmentEvent)
        => new[]
        {
            CreateVariable(options.EventCodeVid, "EquipmentEventCode", SecsItem.A(equipmentEvent.Code)),
            CreateVariable(options.EventMessageVid, "EquipmentEventMessage", SecsItem.A(equipmentEvent.Message)),
            CreateVariable(options.EventKindVid, "EquipmentEventKind", SecsItem.A(equipmentEvent.Kind.ToString())),
            CreateVariable(options.EventSeverityVid, "EquipmentEventSeverity", SecsItem.A(equipmentEvent.Severity.ToString())),
            CreateVariable(options.EventSourceVid, "EquipmentEventSource", SecsItem.A(equipmentEvent.Source ?? string.Empty))
        };

    private void EnsureEventReportRegistered(
        uint ceid,
        string eventName,
        uint rptid,
        IReadOnlyList<GemVariable> variables)
    {
        foreach (var variable in variables)
        {
            registry.RegisterVariable(variable);
            registry.RegisterVariableDefinition(new GemVariableDefinition(
                new GemVid(variable.Id),
                variable.Name,
                GemVariableKind.StatusVariable,
                variable.Unit));
        }

        var variableIds = variables.Select(variable => variable.Id).ToArray();
        registry.RegisterReport(new GemReport(rptid, variableIds));
        registry.RegisterReportDefinition(new GemReportDefinition(
            new GemRptid(rptid),
            variableIds.Select(id => new GemVid(id)).ToArray()));
        registry.RegisterEvent(new GemCollectionEvent(ceid, eventName, new[] { rptid }));
        registry.RegisterEventDefinition(new GemCollectionEventDefinition(
            new GemCeid(ceid),
            eventName,
            new[] { new GemRptid(rptid) }));
    }

    private static GemVariable CreateVariable(uint id, string name, SecsItem value)
        => new(id, name, value);

    private void OnStateChanged(object? sender, EquipmentStateChangedEventArgs e)
    {
        _ = ReportStateChangedSafelyAsync(e);
    }

    private async Task ReportStateChangedSafelyAsync(EquipmentStateChangedEventArgs args)
    {
        try
        {
            await ReportStateChangedAsync(args);
        }
        catch
        {
            // 事件回调不能把异常抛回状态机；通信失败由 GEM/SECS 层自身状态和重连机制处理。
        }
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(GemEquipmentBridge));
        }
    }
}
