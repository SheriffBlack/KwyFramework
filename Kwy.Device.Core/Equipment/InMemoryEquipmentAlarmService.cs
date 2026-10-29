using System.Collections.Concurrent;
using Kwy.Device.Abstractions.Equipment;

namespace Kwy.Device.Core.Equipment;

/// <summary>在进程内维护活动报警，并将报警生命周期发布为整机事件。</summary>
public sealed class InMemoryEquipmentAlarmService : IEquipmentAlarmService
{
    private readonly ConcurrentDictionary<string, EquipmentAlarm> activeAlarms =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly IEquipmentEventSink eventSink;

    public InMemoryEquipmentAlarmService(IEquipmentEventSink eventSink)
    {
        this.eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
    }

    public IReadOnlyCollection<EquipmentAlarm> ActiveAlarms => activeAlarms.Values.ToArray();

    public Task RaiseAsync(EquipmentAlarm alarm, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(alarm);
        cancellationToken.ThrowIfCancellationRequested();

        EquipmentAlarm active = alarm with
        {
            State = EquipmentAlarmState.Active,
            Timestamp = alarm.Timestamp ?? DateTimeOffset.Now
        };
        activeAlarms[active.Code] = active;
        return PublishAsync(active, cancellationToken);
    }

    public async Task ClearAsync(string code, string? reason = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        cancellationToken.ThrowIfCancellationRequested();

        if (!activeAlarms.TryRemove(code, out EquipmentAlarm? alarm))
        {
            return;
        }

        await eventSink.PublishAsync(new EquipmentEvent(
            code,
            reason ?? $"Alarm {code} cleared.",
            EquipmentEventKind.Alarm,
            EquipmentEventSeverity.Information,
            alarm.Source,
            Properties: alarm.Properties), cancellationToken).ConfigureAwait(false);
    }

    public async Task AcknowledgeAsync(
        string code,
        string operatorName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(operatorName);
        cancellationToken.ThrowIfCancellationRequested();

        if (!activeAlarms.TryGetValue(code, out EquipmentAlarm? alarm))
        {
            return;
        }

        activeAlarms[code] = alarm with { State = EquipmentAlarmState.Acknowledged };
        await eventSink.PublishAsync(new EquipmentEvent(
            code,
            $"Alarm {code} acknowledged by {operatorName}.",
            EquipmentEventKind.Audit,
            EquipmentEventSeverity.Information,
            alarm.Source), cancellationToken).ConfigureAwait(false);
    }

    private Task PublishAsync(EquipmentAlarm alarm, CancellationToken cancellationToken)
        => eventSink.PublishAsync(new EquipmentEvent(
            alarm.Code,
            alarm.Message,
            EquipmentEventKind.Alarm,
            alarm.Severity,
            alarm.Source,
            alarm.Timestamp,
            alarm.Properties), cancellationToken);
}
