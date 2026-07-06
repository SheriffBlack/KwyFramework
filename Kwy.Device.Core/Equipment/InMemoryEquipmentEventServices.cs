using Kwy.Device.Abstractions.Equipment;
using System.Collections.Concurrent;

namespace Kwy.Device.Core.Equipment;

public sealed class InMemoryEquipmentEventSink : IEquipmentEventSink
{
    private readonly ConcurrentQueue<EquipmentEvent> events = new();

    public IReadOnlyCollection<EquipmentEvent> Events => events.ToArray();

    public Task PublishAsync(EquipmentEvent equipmentEvent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        events.Enqueue(equipmentEvent with { Timestamp = equipmentEvent.Timestamp ?? DateTimeOffset.Now });
        return Task.CompletedTask;
    }
}

public sealed class InMemoryAlarmService : IAlarmService
{
    private readonly ConcurrentDictionary<string, EquipmentAlarm> activeAlarms = new();
    private readonly IEquipmentEventSink eventSink;

    public InMemoryAlarmService(IEquipmentEventSink eventSink)
    {
        this.eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
    }

    public IReadOnlyCollection<EquipmentAlarm> ActiveAlarms => activeAlarms.Values.ToArray();

    public async Task RaiseAsync(EquipmentAlarm alarm, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var active = alarm with
        {
            State = EquipmentAlarmState.Active,
            Timestamp = alarm.Timestamp ?? DateTimeOffset.Now
        };
        activeAlarms[active.Code] = active;
        await eventSink.PublishAsync(new EquipmentEvent(
            active.Code,
            active.Message,
            active.Severity,
            EquipmentEventKind.Alarm,
            active.Source,
            active.Timestamp,
            active.Properties), cancellationToken);
    }

    public async Task ClearAsync(string code, string? reason = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        cancellationToken.ThrowIfCancellationRequested();

        if (activeAlarms.TryRemove(code, out var alarm))
        {
            await eventSink.PublishAsync(new EquipmentEvent(
                code,
                reason ?? $"Alarm {code} cleared.",
                EquipmentEventSeverity.Information,
                EquipmentEventKind.Alarm,
                alarm.Source), cancellationToken);
        }
    }

    public async Task AcknowledgeAsync(string code, string operatorName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(operatorName);
        cancellationToken.ThrowIfCancellationRequested();

        if (activeAlarms.TryGetValue(code, out var alarm))
        {
            activeAlarms[code] = alarm with { State = EquipmentAlarmState.Acknowledged };
            await eventSink.PublishAsync(new EquipmentEvent(
                code,
                $"Alarm {code} acknowledged by {operatorName}.",
                EquipmentEventSeverity.Information,
                EquipmentEventKind.Audit,
                alarm.Source), cancellationToken);
        }
    }
}

public sealed class InMemoryAuditTrail : IAuditTrail
{
    private readonly ConcurrentQueue<EquipmentAuditRecord> records = new();
    private readonly IEquipmentEventSink eventSink;

    public InMemoryAuditTrail(IEquipmentEventSink eventSink)
    {
        this.eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
    }

    public IReadOnlyCollection<EquipmentAuditRecord> Records => records.ToArray();

    public async Task RecordAsync(EquipmentAuditRecord record, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var timestamped = record with { Timestamp = record.Timestamp ?? DateTimeOffset.Now };
        records.Enqueue(timestamped);
        await eventSink.PublishAsync(new EquipmentEvent(
            timestamped.Action,
            timestamped.Message,
            EquipmentEventSeverity.Information,
            EquipmentEventKind.Audit,
            timestamped.Source,
            timestamped.Timestamp,
            timestamped.Properties), cancellationToken);
    }
}
